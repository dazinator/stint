namespace Stint
{
    using System;
    using System.Collections.Generic;
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
                    // but it could be that it didn't run because of a transient issue, so give some time for this issue to clear.

                    continue;
                }

                // to prevent tight loop of executions in case WaitOneAsync() throws constantly.
                // Important: We don't put this before WaitOneAsync because we want the JobRunner to grab a change token asap, so that IJobManualTriggerInvoker can trigger the job.
                // If we trigger a job before the JobRunner has a change token, the signal will be lost.
                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            _logger.LogWarning("Job runner cancelled.");
        }

        private async Task<bool> RunJobOnce(CancellationToken token)
        {
            // Console.WriteLine($"Received: {item}");
            if (token.IsCancellationRequested)
            {
                _logger.LogInformation("Cancelled..");
                return false;
            }

            try
            {
                // run now!

                // wait for a lock, keep trying to aquire it in periods
                //var lockAttemptCount = 0;
                using var acquiredLock = await WaitForLockWithIncreasingDelays(token, (attemptCount) =>
                    {
                       // lockAttemptCount = attemptCount;
                        return TimeSpan.FromSeconds(attemptCount);
                    },
                    1);
                if (acquiredLock == null)
                {
                    // if we are unable to acquire the lock, we take this as a sign that the job is already running - perhaps on another instance in a distributed scenario.
                    // therefore this isn't necessarily an error, so we log it as a warning.
                    // We infer from lock acquisition failure that another instance of the job is running, so we can also await this lock to be released before to detect when this other instance has finished
                    // and can try to reload the anchor that the other instance will have updated inside its lock upon completion.
                    _logger.LogWarning("Unable to acquire lock");
                    return false;
                }

                // We are inside the lock, let's check if the anchor has changed since we last loaded it. This would be a sign that another instance of the job has run and updated the anchor, since our signal.
                var isAnchorValid = await CheckAnchorHasNotBeenModified(token);
                if (!isAnchorValid)
                {
                    // We log warning and skip executing the job again.
                    _logger.LogWarning("Job anchor has changed, perhaps job executed by another process.");
                    return false;
                }

                var jobInfo = new ExecutionInfo(Name);
                // TODO: Add options for retrying when failure.
                await ExecuteJob(Config.Type, jobInfo, token);
                Anchor = await _anchorStore.DropAnchorAsync(token);
                _publisher.Publish(this, new JobCompletedEventArgs(this.Name));
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
                _logger.LogError(e, "Job errored");
                return false;
            }
        }

        private async Task<IDisposable> WaitForLockWithIncreasingDelays(CancellationToken token, Func<int, TimeSpan> getLockAcquisitionTimeout, int delayIntervalInMinsBeforeRetry = 1)
        {
            //const int attemptIntervalMinutes = 1; // Define how often to retry acquiring the lock

            var attemptCount = 0;

            while (!token.IsCancellationRequested)
            {
                attemptCount = attemptCount + 1;
                var lockAcquisitionAttemptTimeout = getLockAcquisitionTimeout(attemptCount);
                using var timeoutCts = new CancellationTokenSource(lockAcquisitionAttemptTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

                var acquiredLock = await _lockProvider.TryAcquireAsync(Name, linkedCts.Token);
                if (acquiredLock == null)
                {
                    // unable to acquire lock, keep waiting
                    _logger.LogInformation("Unable to acquire lock, another instance might be running. Retrying in {0} min.", delayIntervalInMinsBeforeRetry);
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
