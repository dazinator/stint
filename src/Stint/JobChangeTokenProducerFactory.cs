namespace Stint
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Stint.Triggers;

    public class JobChangeTokenProducerFactory : IJobChangeTokenProducerFactory
    {

        private readonly ILogger<JobChangeTokenProducerFactory> _logger;
        private readonly ITriggerProvider[] _triggerProviders;

        public JobChangeTokenProducerFactory(
            ILogger<JobChangeTokenProducerFactory> logger,
            IEnumerable<ITriggerProvider> triggerProviders)
        {
            _logger = logger;
            _triggerProviders = triggerProviders?.ToArray();
        }

        /// <summary>
        /// Build an <see cref="IChangeTokenProducer"/> for a job that will produce <see cref="IChangeToken"/>'s that will be signalled when the job needs to be executed.
        /// </summary>
        /// <param name="jobName"></param>
        /// <param name="jobConfig"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public IChangeTokenProducer GetChangeTokenProducer(
            string jobName,
            JobConfig jobConfig,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Building change token for job: {jobname}", jobName);
            var tokenProducerBuilder = new ChangeTokenProducerBuilder();
            // allow trigger providers to include their own ChangeToken's in the composite.
            // trigger providers is an extension point, so that we can support novel ways of triggering jobs.
            // examples are: Schedule (e.g cron) and Manual invoke.
            foreach (var triggerProvider in _triggerProviders)
            {
                triggerProvider.AddTriggerChangeTokens(jobName, jobConfig, tokenProducerBuilder, cancellationToken);
            }

            var tokenProducer = tokenProducerBuilder.Build();
            return tokenProducer;
        }
    }
}
