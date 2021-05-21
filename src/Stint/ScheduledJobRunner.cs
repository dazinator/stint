namespace Stint
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Cronos;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Utils;

    public class ScheduledJobRunner : IDisposable
    {
        private readonly JobAnchorStore _anchorStore;
        private readonly ILogger<ScheduledJobRunner> _logger;
        private readonly IJobSettingsStore _optionsStore;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ScheduledJobRunner(
            string name,
            JobConfig jobConfig,
            JobAnchorStore anchorStore,
            IJobSettingsStore optionsStore,
            ILogger<ScheduledJobRunner> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            Name = name;
            JobConfig = jobConfig;
            _anchorStore = anchorStore;
            _optionsStore = optionsStore;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public CronExpression CronExpression { get; set; }
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
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return RunToScheduleAsync(CancellationTokenSource.Token);
        }

        public async Task RunToScheduleAsync(CancellationToken token)
        {
            // TODO: Valid cron expression should be parsed and passed in as dependency. rather that doing this here.
            // If its wrong the job should not be created.
            var expression = CronExpression.Parse(JobConfig.Schedule);
            // DateTime? previousOccurrence = null;

            while (!token.IsCancellationRequested)
            {
                // if (previousOccurrence == null)
                // {
                var now = DateTime.UtcNow;
                var previousOccurrence = await _anchorStore.GetAnchorAsync(token);
                // DateTime? missedOccurrence = null;
                //bool overdue = false;
                //}
                // if (previousOccurrence != null)
                // {
                //     var firstMissedOccurrence = expression.GetOccurrences(previousOccurrence.Value, now, false, true).FirstOrDefault();
                //     if (firstMissedOccurrence != default)
                //     {
                //         missedOccurrence = firstMissedOccurrence;
                //     }
                // }

                var fromWhenShouldItNextRun =
                    previousOccurrence ?? DateTime.UtcNow; // if we have never run before, get next occurrence from now!

                var occurrence = expression.GetNextOccurrence(fromWhenShouldItNextRun);
                if (occurrence == null)
                {
                    // job won't occur again based on this schedule..
                    // this job is over..
                    return;
                }


                // if the next occurrence is in the past


                // calculate time in milliseconds from now until then.
                // if it's in the future, wait until then!
                var difference = occurrence.Value - now;
                if (difference.TotalMilliseconds > 0)
                {
                    // wait until next occurence.
                    // TimeSpan.FromMilliseconds(difference.TotalMilliseconds);
                    _logger.LogInformation("Waiting for {timespan}", difference);

                    // Task.Delay only allows a max timespan of approx 25 days. If delay is longer than that it will throw.
                    // So here we allow the delay to be broken into multiple delays where it could be longer than the max a single Task.Delay can deal with.
                    var totalMs = (long)difference.TotalMilliseconds;
                    await LongDelay.For(totalMs, token);
                    //  await Task.Delay(difference);
                    // // var delayInMs = (long)difference.TotalMilliseconds;
                    // await LongDelay.For(, token, (ms) =>
                    // {

                    // });
                    if (token.IsCancellationRequested)
                    {
                        _logger.LogWarning("Job cancelled");
                        return;
                    }
                }

                // run now!
                var jobInfo = new ExecutionInfo(Name, previousOccurrence, occurrence.Value, _optionsStore);

                // TODO: Add options for retrying when failure.
                await ExecuteScheduledJob(JobConfig.Type, jobInfo, token);
                var newAnchor = await _anchorStore.DropAnchorAsync(token);
            }
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
                }
            }
        }
    }
}
