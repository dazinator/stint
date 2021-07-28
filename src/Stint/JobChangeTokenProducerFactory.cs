namespace Stint
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cronos;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Stint.PubSub;
    public class JobChangeTokenProducerFactory : IJobChangeTokenProducerFactory
    {

        private readonly ILogger<JobChangeTokenProducerFactory> _logger;
        private readonly IAnchorStoreFactory _anchorStoreFactory;
        private readonly ILockProvider _lockProvider;
        private readonly IJobManualTriggerRegistry _manualTriggers;
        private readonly ISubscriber<JobCompletedEventArgs> _subscriber;

        public JobChangeTokenProducerFactory(
            ILogger<JobChangeTokenProducerFactory> logger,
            IAnchorStoreFactory anchorStoreFactory,
            ILockProvider lockProvider,
            IJobManualTriggerRegistry triggers,
            ISubscriber<JobCompletedEventArgs> subscriber)
        {
            _logger = logger;
            _anchorStoreFactory = anchorStoreFactory;
            _lockProvider = lockProvider;
            _manualTriggers = triggers;
            _subscriber = subscriber;
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

            //DateTime? lastReturnedAnchor = null;

            Task<DateTime?> lastAnchorTask = null;

            // Include a producer that just captures a snapshot of the current anchor when a change token is requested.
            tokenProducerBuilder.Include(() =>
            {
                lastAnchorTask = anchorStore.GetAnchorAsync(cancellationToken);
                return EmptyChangeToken.Instance;
            });

            var scheduleTriggerConfigs = jobConfig.Triggers?.Schedules;
            if (scheduleTriggerConfigs?.Any() ?? false)
            {
                foreach (var scheduleTriggerConfig in scheduleTriggerConfigs)
                {
                    var expression = CronExpression.Parse(scheduleTriggerConfig.Schedule);
                    tokenProducerBuilder.IncludeDatetimeScheduledTokenProducer(async () =>
                    {
                        // This token producer will signal tokens at the specified datetime. Will calculate the next datetime a job should run based on looking at when it last ran, and its schedule etc.
                        var previousOccurrence = await lastAnchorTask;

                        //  await anchorStore.GetAnchorAsync(cancellationToken);
                        //  lastReturnedAnchor = previousOccurrence;
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

            if (jobConfig?.Triggers?.Manual ?? false)
            {
                // register a delegate that can trigger this job, by the job name.
                tokenProducerBuilder.IncludeTrigger(out var triggerDelegate);
                _manualTriggers.AddUpdateTrigger(jobName, triggerDelegate);
            }

            // If job is configured to run when other jobs complete,
            // then subscribe to job completion events, and when an event is received, if the job name that completed
            // matches the job name that should trigger this job, then invoke the trigger for this job!
            var onJobCompletedTriggers = jobConfig.Triggers?.JobCompletions;
            if (onJobCompletedTriggers?.Any() ?? false)
            {
                tokenProducerBuilder.IncludeSubscribingHandlerTrigger((trigger) => _subscriber.Subscribe((s, e) =>
                {
                    foreach (var jobCompletedTrigger in onJobCompletedTriggers)
                    {
                        if (string.Equals(jobCompletedTrigger.JobName, e.Name))
                        {
                            trigger?.Invoke();
                            break;
                        }
                    }
                }));
            }


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
                 // we are now within the aquired lock, doing a final check before we signal.
                 // with scheduled jobs, the occurrence is calculated based on the last anchor.
                 // so if we are about to execute due to a scheduled trigger, it should be the case that anchor used to
                 // calclulate this occurrence to begin with, is still the same. If it has changed, it should render this occurrence invalid.
                 // to be safe we should do this in the case of all triggers - if the anchor has changed since the trigger fired, assume the occorrence is now invalid,
                 // as the job might have just run somewhere else, by another process etc.
                 var originalAnchor = await lastAnchorTask;
                 var latestAnchor = await anchorStore.GetAnchorAsync(cancellationToken);
                 return latestAnchor == originalAnchor;
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
