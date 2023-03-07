namespace Stint.Triggers.Schedule
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cronos;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Stint;
    using Stint.Triggers;

    public class ScheduleTriggerProvider : ITriggerProvider
    {
        private readonly ILogger<ScheduleTriggerProvider> _logger;

        public ScheduleTriggerProvider(ILogger<ScheduleTriggerProvider> logger) => _logger = logger;

        public void AddTriggerChangeTokens(
          string jobName,
          JobConfig jobConfig,
          Func<Task<DateTime?>> lastRanAnchorTaskFactory,
          ChangeTokenProducerBuilder builder,
          CancellationToken cancellationToken)
        {

            var scheduleTriggerConfigs = jobConfig.Triggers?.Schedules;
            if (scheduleTriggerConfigs?.Any() ?? false)
            {
                foreach (var scheduleTriggerConfig in scheduleTriggerConfigs)
                {
                    var expression = CronExpression.Parse(scheduleTriggerConfig.Schedule);
                    builder.IncludeDatetimeScheduledTokenProducer(async () =>
                    {
                        // This token producer will signal tokens at the specified datetime. Will calculate the next datetime a job should run based on looking at when it last ran, and its schedule etc.
                        var lastRunAnchorFactory = lastRanAnchorTaskFactory();
                        var previousOccurrence = await lastRunAnchorFactory;

                        //  await anchorStore.GetAnchorAsync(cancellationToken);
                        //  lastReturnedAnchor = previousOccurrence;
                        if (previousOccurrence == null)
                        {
                            _logger.LogInformation("Job {jobname} has not previously run", jobName);
                        }

                        var fromWhenShouldItNextRun =
                            previousOccurrence ?? DateTime.UtcNow; // if we have never run before, get next occurrence from now therwise get next occurrence from when it last ran!

                        var nextOccurence = expression.GetNextOccurrence(fromWhenShouldItNextRun);
                        _logger.LogInformation("Next occurrence of {jobname} is @ {nextOccurence} using cron {cronSchedule}", jobName, nextOccurence, scheduleTriggerConfig.Schedule);
                        return nextOccurence;
                    }, cancellationToken,
                    () => _logger.LogWarning("Mo more occurrences for job {jobName}", jobName),
                    (delayMs) => _logger.LogDebug("Will delay for {delayMs} ms to execute {jobName}.", delayMs, jobName));
                }
            }
        }
    }
}
