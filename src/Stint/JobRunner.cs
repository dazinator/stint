namespace Stint
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Stint.PubSub;

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

            while (!token.IsCancellationRequested && !Disabled)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1)); // to prevent tight loop of exceptions in case WaitOneAsync() throws constantly.

                    // get the current version of the anchor.
                    await LoadAnchor(token);

                    await _changeTokenProducer.WaitOneAsync(token); // wait for a change token to be signalled.
                    if (token.IsCancellationRequested)
                    {
                        continue;
                    }

                    // run now!
                    var jobRan = await ExecuteJobWithinLock(_lockProvider, _anchorStore, _publisher, token);
                    if (!jobRan)
                    {
                        // the job could not be run - it is likely already running.
                        // we should wait for the next signal.
                        _logger.LogWarning("Job did not execute. Will wait for next signal.");
                        continue;
                    }

                    _logger.LogInformation("Job completed.");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Job errored");
                }
            }

            _logger.LogInformation("Job cancelled");
        }

        private async Task LoadAnchor(CancellationToken token)
        {
            Anchor = await _anchorStore.GetAnchorAsync(token);
        }

        private async Task<bool> ExecuteJobWithinLock(ILockProvider lockProvider, IAnchorStore anchorStore, IPublisher<JobCompletedEventArgs> publisher, CancellationToken token)
        {
            // aquire lock on job name.
            // Each configuration of a job has a unique name.

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var lockAcquisitionTimeout = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

            using var acquiredLock = await lockProvider.TryAcquireAsync(Name, lockAcquisitionTimeout.Token);
            if (acquiredLock == null)
            {
                // if we are unable to acquire the lock, we take this as a sign that the job is already running - perhaps on another instance in a distributed scenario.
                // therefore this isn't necessarily an error, so we log it as a warning.
                _logger.LogWarning("Unable to acquire lock");
                return false;
            }

            // we now have the lock, but if the anchor has changed since we last loaded it,
            // then we should not run the job as it means the anchor was updated by another instance in between, and so our initial conditions for triggering this job are no longer valid.
            var isAnchorValid = await CheckIsAnchorValid(token);
            if (!isAnchorValid)
            {
                _logger.LogWarning("Job anchor has changed, perhaps job executed by another process.");
                return false;
            }


            var jobInfo = new ExecutionInfo(Name);
            // TODO: Add options for retrying when failure.
            await ExecuteJob(Config.Type, jobInfo, token);
            Anchor = await anchorStore.DropAnchorAsync(token);
            publisher.Publish(this, new JobCompletedEventArgs(this.Name));
            return true;
        }

        private async Task<bool> CheckIsAnchorValid(CancellationToken token)
        {
            var latestAnchor = await _anchorStore.GetAnchorAsync(token);
            return latestAnchor == Anchor;
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
