namespace Stint
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection.Metadata.Ecma335;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Stint.Triggers;

    public class JobChangeTokenProducerFactory : IJobChangeTokenProducerFactory
    {

        private readonly ILogger<JobChangeTokenProducerFactory> _logger;
        private readonly IAnchorStoreFactory _anchorStoreFactory;
        private readonly ILockProvider _lockProvider;
        private readonly ITriggerProvider[] _triggerProviders;

        public JobChangeTokenProducerFactory(
            ILogger<JobChangeTokenProducerFactory> logger,
            IAnchorStoreFactory anchorStoreFactory,
            ILockProvider lockProvider,

            IEnumerable<ITriggerProvider> triggerProviders)
        {
            _logger = logger;
            _anchorStoreFactory = anchorStoreFactory;
            _lockProvider = lockProvider;
            _triggerProviders = triggerProviders?.ToArray();
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
            _logger.LogDebug("Building change token for job: {jobname}", jobName);
            var anchorStore = GetAnchorStore(jobName);

            var tokenProducerBuilder = new ChangeTokenProducerBuilder();

            //DateTime? lastReturnedAnchor = null;

            Task<DateTime?> lastAnchorTask = null;
            Func<Task<DateTime?>> getLastAnchorTaskFactory = () => lastAnchorTask;

            // Include a producer that just captures a snapshot of the current anchor when a change token is requested.
            // this is so we can double check is hasn't been modified within the lock.
            tokenProducerBuilder.Include(() =>
            {
                _logger.LogDebug("Getting snapshot of anchor for {jobname}", jobName);
                lastAnchorTask = anchorStore.GetAnchorAsync(cancellationToken);
                return EmptyChangeToken.Instance;
            });

            // allow trigger providers to include their own ChangeToken's in the composite.
            // trigger providers is an extension point, so that we can support novel ways of triggering jobs.
            // examples are: Schedule (e.g cron) and Manual invoke.
            foreach (var triggerProvider in _triggerProviders)
            {
                triggerProvider.AddTriggerChangeTokens(jobName, jobConfig, getLastAnchorTaskFactory, tokenProducerBuilder, cancellationToken);
            }

            var tokenProducer = tokenProducerBuilder.Build()
             .AndResourceAcquired(async () =>
             {
                 var aquiredLock = await _lockProvider.TryAcquireAsync(jobName);
                 if (aquiredLock == null)
                 {
                     // if lock cannot be aquired, delay for atleast a minute to prevent further attempts within this period - as
                     // // inner token may be singalled and without this delay, the consumer may immediately re-attempt.
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
             }, () => _logger.LogInformation("Job anchor has changed, perhaps job executed by another process."))
             .Build();

            return tokenProducer;
        }


        private IAnchorStore GetAnchorStore(string name) => _anchorStoreFactory.GetAnchorStore(name);
    }
}
