namespace Stint
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cronos;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    public class JobChangeTokenProducerFactory : IJobChangeTokenProducerFactory
    {

        private readonly ILogger<JobChangeTokenProducerFactory> _logger;
        private readonly IAnchorStoreFactory _anchorStoreFactory;
        private readonly ILockProvider _lockProvider;


        public JobChangeTokenProducerFactory(ILogger<JobChangeTokenProducerFactory> logger,
            IAnchorStoreFactory anchorStoreFactory,
            ILockProvider lockProvider)
        {
            _logger = logger;
            _anchorStoreFactory = anchorStoreFactory;
            _lockProvider = lockProvider;
        }

        /// <summary>
        /// Build an <see cref="IChangeTokenProducer"/> for a job that will produce <see cref="IChangeToken"/>'s that will be signalled when the job needs to be executed.
        /// </summary>
        /// <param name="jobName"></param>
        /// <param name="jobConfig"></param>
        /// <param name="anchorStore"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IChangeTokenProducer GetChangeTokenProducer(
            string jobName,
            JobConfig jobConfig,
            CancellationToken cancellationToken)
        {
            var anchorStore = GetAnchorStore(jobName);

            var tokenProducerBuilder = new ChangeTokenProducerBuilder();
            DateTime? lastReturnedAnchor = null;

            var scheduleTriggerConfigs = jobConfig.Triggers?.Schedules;
            if (scheduleTriggerConfigs?.Any() ?? false)
            {
                foreach (var scheduleTriggerConfig in scheduleTriggerConfigs)
                {
                    var expression = CronExpression.Parse(scheduleTriggerConfig.Schedule);
                    tokenProducerBuilder.IncludeDatetimeScheduledTokenProducer(async () =>
                    {
                        // This token producer will signal tokens at the specified datetime. Will calculate the next datetime a job should run based on looking at when it last ran, and its schedule etc.
                        var previousOccurrence = await anchorStore.GetAnchorAsync(cancellationToken);
                        lastReturnedAnchor = previousOccurrence;
                        if (previousOccurrence == null)
                        {
                            _logger.LogInformation("Job has not previously run");
                        }

                        var fromWhenShouldItNextRun =
                            previousOccurrence ?? DateTime.UtcNow; // if we have never run before, get next occurrence from now therwise get next occurrence from when it last ran!

                        var nextOccurence = expression.GetNextOccurrence(fromWhenShouldItNextRun);
                        _logger.LogInformation("Next occurrence {nextOccurence}", nextOccurence);
                        return nextOccurence;
                    }, cancellationToken,
                    () => _logger.LogWarning("Mo more occurrences for job."),
                    (delayMs) => _logger.LogDebug("Will delay for {delayMs} ms.", delayMs));
                }
            }


            //  var expression = CronExpression.Parse(jobConfig.Schedule);




            //var tokenProducer = tokenProducerBuilder
            //    .IncludeDatetimeScheduledTokenProducer(async () =>
            //    {
            //        // This token producer will signal tokens at the specified datetime. Will calculate the next datetime a job should run based on looking at when it last ran, and its schedule etc.
            //        var previousOccurrence = await anchorStore.GetAnchorAsync(cancellationToken);
            //        lastReturnedAnchor = previousOccurrence;
            //        if (previousOccurrence == null)
            //        {
            //            _logger.LogInformation("Job has not previously run");
            //        }

            //        var fromWhenShouldItNextRun =
            //            previousOccurrence ?? DateTime.UtcNow; // if we have never run before, get next occurrence from now therwise get next occurrence from when it last ran!

            //        var nextOccurence = expression.GetNextOccurrence(fromWhenShouldItNextRun);
            //        _logger.LogInformation("Next occurrence {nextOccurence}", nextOccurence);
            //        return nextOccurence;
            //    }, cancellationToken,
            //    () => _logger.LogWarning("Mo more occurrences for job."),
            //    (delayMs) => _logger.LogInformation("Will delay for {delayMs} ms.", delayMs))
            var tokenProducer = tokenProducerBuilder.Build()
                 .AndResourceAcquired(async () =>
                 {
                     var aquiredLock = await _lockProvider.TryAcquireAsync(jobName);
                     if (aquiredLock == null)
                     {
                         // if lock cannot be aquired, delay for atleast a minute to prevent further attempts within this period - as
                         // // inner token may be singalled and without this delay, this token provider would immeidately re-attempt.
                         await Task.Delay(TimeSpan.FromMinutes(1));
                     }
                     return aquiredLock;
                 }, // omit signal if lock cannot be acquired.
                     () => _logger.LogInformation("Job {JobName} was not triggered as lock could not be obtained, another instance may already be running.", jobName))
                 .Build()
                 .AndTrueAsync(async () => // omit signal if this delegate check does not return true.
                 {
                     var latestAnchor = await anchorStore.GetAnchorAsync(cancellationToken);
                     if (latestAnchor == lastReturnedAnchor)
                     {
                         return true;
                     }
                     else
                     {
                         return false;
                     }
                 }, () =>
                 {
                     _logger.LogInformation("Job anchor has changed, perhaps job executed by another process.");
                 })
                 .Build();

            return tokenProducer;
        }


        private IAnchorStore GetAnchorStore(string name) => _anchorStoreFactory.GetAnchorStore(name);


    }
}
