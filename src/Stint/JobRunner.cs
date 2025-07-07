namespace Stint
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using PubSub;

    public class JobRunner : IJobRunner
    {
        private readonly ILockProvider _lockProvider;
        private readonly IAnchorStore _anchorStore;
        private readonly ILogger<JobRunner> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IChangeTokenProducer _changeTokenProducer;
        private readonly IPublisher<JobCompletedEventArgs> _publisher;

        private static readonly ActivitySource ActivitySource = new("Stint");

        public JobRunner(
            string name,
            ILockProvider lockProvider,
            JobConfig config,
            IAnchorStore anchorStore,
            ILogger<JobRunner> logger,
            IServiceScopeFactory serviceScopeFactory,
            IChangeTokenProducer changeTokenProducer,
            IPublisher<JobCompletedEventArgs> publisher
        )
        {
            Name = name;
            Config = config;
            _lockProvider = lockProvider;
            _anchorStore = anchorStore;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _changeTokenProducer = changeTokenProducer;
            _publisher = publisher;
        }

        private CancellationTokenSource CancellationTokenSource { get; set; }
        public string Name { get; }
        public JobConfig Config { get; }

        public DateTime? Anchor { get; private set; }

        public void Dispose()
        {
            CancellationTokenSource?.Cancel();
            CancellationTokenSource?.Dispose();
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return ExecuteWhenSignalledAsync(CancellationTokenSource.Token);
        }

        private async Task ExecuteWhenSignalledAsync(CancellationToken token)
        {
            // DateTime? previousOccurrence = null;
            using var _jobScope = _logger.BeginScope(new Dictionary<string, object>
            {
                {
                    "StintJobName", Name
                },
            });


            // get the current version of the anchor.
            _logger.LogDebug("Loading Anchor..");
            await LoadAnchor(token);

            while (!token.IsCancellationRequested && !Disabled)
            {
                await _changeTokenProducer.WaitOneAsync(token);
                var ran = await RunJobOnce(token);
                if (!ran)
                {
                    // if the job did not run, we should wait for the next signal.
                    continue;
                }

                // We ran. To prevent tight loop of executions in case WaitOneAsync() throws constantly or in case job cron is constantly triggering and job is instantly running, we add a delay.
                // Important: We deliberately don't put this delay at the start of the loop, before WaitOneAsync() above because we want the JobRunner to grab a change token asap, so that IJobManualTriggerInvoker can trigger the job via that change token.
                // If we trigger a job before the JobRunner has grabbed a change token, that  signal will be lost. (TODO this could be fixed in future by queing the signal from IJobManualTriggerInvoker and processing it later)
                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            _logger.LogWarning("Job runner cancelled.");
        }

        private const string ActivityNameRunJobOnce = "JobRunner.RunJobOnce";
        private const string ActivityNameWaitForLock = "JobRunner.WaitForLock";
        private async Task<bool> RunJobOnce(CancellationToken token)
        {

            using var activity = ActivitySource.StartActivity(
                ActivityNameRunJobOnce,
                ActivityKind.Internal,
                parentContext: default // explicitly no parent
            );
            activity?.SetTag("job.name", Name);
            activity?.SetTag("job.type", Config?.Type);

            if (token.IsCancellationRequested)
            {
                _logger.LogInformation("Cancelled..");
                activity?.SetStatus(ActivityStatusCode.Error, "Cancelled");                        
                return false;
            }

            try
            {
                // run now!

                // wait for a lock, keep trying to aquire it in periods
                //var lockAttemptCount = 0;
                using var lockActivity = ActivitySource.StartActivity(ActivityNameWaitForLock, ActivityKind.Internal);
                using var acquiredLock = await WaitForLockWithIncreasingDelays(token, (attemptCount) =>
                    {
                        // lockAttemptCount = attemptCount;
                        var multiplier = Math.Max(attemptCount, 10);
                        var timeoutSecs = multiplier * 10;
                        return TimeSpan.FromSeconds(timeoutSecs);
                    },
                    1);

                lockActivity?.SetTag("lock.acquired", acquiredLock != null);
                if (acquiredLock == null)
                {
                    // if we are unable to acquire the lock, we take this as a sign that the job is already running - perhaps on another instance in a distributed scenario.
                    // therefore this isn't necessarily an error, so we log it as a warning.
                    _logger.LogWarning("Unable to acquire lock");
                    activity?.SetStatus(ActivityStatusCode.Ok, "Lock not acquired");                   
                    return false;
                }

                // We are inside the lock, let's check if the anchor has changed since we last loaded it. This would be a sign that another instance of the job has run and updated the anchor, since our signal.
                var isAnchorValid = await CheckAnchorHasNotBeenModified(token);
                activity?.SetTag("anchor.valid", isAnchorValid);
                if (!isAnchorValid)
                {
                    // We log warning and skip executing the job again.
                    _logger.LogWarning("Job anchor has changed, perhaps job executed by another process.");
                    activity?.SetStatus(ActivityStatusCode.Ok, "Anchor changed - skip");
                    activity?.SetTag("job.outcome", "skipped");
                    return false;
                }

                var jobInfo = new ExecutionInfo(Name);
                // TODO: Add options for retrying when failure.
                using var jobExecActivity = ActivitySource.StartActivity("Job.Execute", ActivityKind.Internal);
                await ExecuteJob(Config.Type, jobInfo, token);
                jobExecActivity?.SetStatus(ActivityStatusCode.Ok);

                Anchor = await _anchorStore.DropAnchorAsync(token);
                _publisher.Publish(this, new JobCompletedEventArgs(this.Name));
                activity?.SetStatus(ActivityStatusCode.Ok, "Job completed");
                activity?.SetTag("job.outcome", "success");
                activity?.SetTag("job.anchor.updated", Anchor?.ToString("O")); // ISO 8601 format

                _logger.LogInformation("Job completed.");
                return true;

                // var jobRan = await ExecuteJobWithinLock(_lockProvider, _anchorStore, _publisher, token);
                // if (!jobRan)
                // {
                //     // the job could not be run - it is likely already running.
                //     // we should wait for the next signal.
                //     _logger.LogWarning("Job did not execute. Will wait for next signal.");
                //     return false;
                // }


            }
            catch (Exception e)
            {
                activity?.SetStatus(ActivityStatusCode.Error, e.Message);
                activity?.RecordException(e);         
                _logger.LogError(e, "Job errored");
                activity?.SetTag("job.outcome", "failure");
                return false;
            }
        }

        private async Task<IDisposable> WaitForLockWithIncreasingDelays(CancellationToken token, Func<int, TimeSpan> getLockAcquisitionTimeout, int delayIntervalInMinsBeforeRetry = 1)
        {
            //const int attemptIntervalMinutes = 1; // Define how often to retry acquiring the lock

            var attemptCount = 0;

            while (!token.IsCancellationRequested)
            {
                attemptCount++;
                var lockAcquisitionAttemptTimeout = getLockAcquisitionTimeout(attemptCount);
                using var timeoutCts = new CancellationTokenSource(lockAcquisitionAttemptTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

                var acquiredLock = await _lockProvider.TryAcquireAsync(Name, linkedCts.Token);
                if (acquiredLock == null)
                {
                    // unable to acquire lock, keep waiting
                    _logger.LogWarning("Unable to acquire lock, another instance might be running. Retrying in {0} min.", delayIntervalInMinsBeforeRetry);
                    await Task.Delay(TimeSpan.FromMinutes(delayIntervalInMinsBeforeRetry), token);
                    continue;
                }


                _logger.LogDebug("Lock acquired.");
                return acquiredLock;
            }

            return null;
        }

        private async Task LoadAnchor(CancellationToken token) => Anchor = await _anchorStore.GetAnchorAsync(token);

        private async Task<bool> CheckAnchorHasNotBeenModified(CancellationToken token)
        {
            var latestAnchor = await _anchorStore.GetAnchorAsync(token);
            var isValid = latestAnchor == Anchor;
            Anchor = latestAnchor;
            return isValid;
        }

        private bool Disabled { get; set; } = false;

        protected virtual async Task ExecuteJob(string jobTypeName, ExecutionInfo runInfo, CancellationToken token)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var factory = scope.ServiceProvider.GetRequiredService<Func<string, IJob>>();

                // Do all the work we need to do!
                IJob job;
                try

                {
                    job = factory.Invoke(jobTypeName);
                    if (job == null)
                    {
                        // no such job registered..
                        _logger.LogWarning("No job type named {name} is registered.", jobTypeName);
                        return;
                    }
                }
                catch (KeyNotFoundException)
                {
                    // if we can't actviate the job, disable it.
                    Disabled = true;
                    // ExceptionCount = ExceptionCount + 1;
                    // unable to find job type specified..
                    _logger.LogWarning("No job type named {name} is registered. This job will be disabled.", jobTypeName);
                    return;
                    // throw;
                }
                catch (Exception ex)
                {
                    // ExceptionCount = ExceptionCount + 1;
                    Disabled = true;
                    _logger.LogError(ex, "Unable to create job type named {name} - it might be missing dependencies. This job will be disabled.",
                        jobTypeName);
                    return;
                }

                await job.ExecuteAsync(runInfo, token);
            }
        }
    }
}
