namespace Stint
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Changify;
    using Cronos;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    public class ScheduledJobRunner : IDisposable
    {
        private readonly IAnchorStore _anchorStore;
        private readonly ILogger<ScheduledJobRunner> _logger;
        private readonly IJobOptionsStore _optionsStore;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILockProvider _lockProvider;

        public ScheduledJobRunner(
                string name,
                JobConfig jobConfig,
                IAnchorStore anchorStore,
                IJobOptionsStore optionsStore,
                ILogger<ScheduledJobRunner> logger,
                IServiceScopeFactory serviceScopeFactory,
                ILockProvider lockProvider)
        {
            Name = name;
            JobConfig = jobConfig;
            _anchorStore = anchorStore;
            _optionsStore = optionsStore;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _lockProvider = lockProvider;
        }

        public CancellationTokenSource CancellationTokenSource { get; set; }
        public string Name { get; }
        public JobConfig JobConfig { get; }

        public void Dispose()
        {
            CancellationTokenSource?.Cancel();
            CancellationTokenSource?.Dispose();
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            // consider
            // Subscribing to a change token producer, signalled by a scheduled trigger.

            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return RunToScheduleAsync(CancellationTokenSource.Token);
        }

        public async Task RunToScheduleAsync(CancellationToken token)
        {
            // DateTime? previousOccurrence = null;

            // TODO: Valid cron expression should be parsed and passed in as dependency. rather that doing this here.
            // If its wrong the job should not be created.
            var expression = CronExpression.Parse(JobConfig.Schedule);
            DateTime? lastReturnedAnchor = null;

            var delayTrigger = new ScheduledChangeTokenProducer(
                async () =>
                {
                    var previousOccurrence = await _anchorStore.GetAnchorAsync(token);
                    lastReturnedAnchor = previousOccurrence;
                    if (previousOccurrence == null)
                    {
                        _logger.LogInformation("Job has not previously run");
                    }

                    var fromWhenShouldItNextRun =
                        previousOccurrence ?? DateTime.UtcNow; // if we have never run before, get next occurrence from now therwise get next occurrence from when it last ran!

                    // var occurrence = getNextOccurrence?.Invoke(fromWhenShouldItNextRun);
                    var nextOccurence = expression.GetNextOccurrence(fromWhenShouldItNextRun);
                    _logger.LogInformation("Next occurrence {nextOccurence}", nextOccurence);
                    return nextOccurence;
                }, token);


            var tokenProducer = new ChangeTokenProducerBuilder()
                .Include(delayTrigger)
                .Build()
                .FilterOnResourceAcquired(async () => await _lockProvider.TryAcquireAsync(Name),
                    () =>
                    {
                        _logger.LogInformation("Job {JobName} was not triggered as lock could not be obtained, another instance may already be running.", Name);
                    })
                .Build(out var producerLifetime);

            while (!token.IsCancellationRequested)
            {
                await tokenProducer.WaitOneAsync(); // wait for a change token to be signalled.
                if (token.IsCancellationRequested)
                {
                    continue;
                }

                // double check if the anchor has changed,
                // if another process just also ran this job, it will have dropped a new anchor point.
                // So we detect that, and only want to continue to execute the job if the anchor hasn't been changed.               
                var latestAnchor = await _anchorStore.GetAnchorAsync(token);
                if (latestAnchor != lastReturnedAnchor)
                {
                    _logger.LogDebug("Job anchor has changed, perhaps job executed by another process.");
                    continue;
                }

                // run now!
                var jobInfo = new ExecutionInfo(Name, _optionsStore);

                // TODO: Add options for retrying when failure.
                await ExecuteScheduledJob(JobConfig.Type, jobInfo, token);
                var newAnchor = await _anchorStore.DropAnchorAsync(token);
            }

            _logger.LogInformation("Job cancelled");
        }


        protected virtual async Task ExecuteScheduledJob(string jobTypeName, ExecutionInfo runInfo,
            CancellationToken token)
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
                    // unable to find job type specified..
                    _logger.LogWarning("No job type named {name} is registered.", jobTypeName);
                    return;
                    // throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to create job type named {name} - it might be missing dependencies.",
                        jobTypeName);
                    return;
                }

                try
                {
                    await job.ExecuteAsync(runInfo, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Job error.");
                    // don't allow job execution exceptions to bubble any further.
                    // return success and log error.
                    // jobs must currently handle their own retry logic..
                }
            }
        }
    }
}
