namespace Stint
{
    using System.Threading;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public class JobRunnerFactory : IJobRunnerFactory
    {
        private readonly IJobChangeTokenProducerFactory _jobChangeTokenProducerFactory;
        private readonly IAnchorStoreFactory _anchorStoreFactory;
        private readonly ILogger<JobRunner> _jobRunnerLogger;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public JobRunnerFactory(
            IJobChangeTokenProducerFactory jobChangeTokenProducerFactory,
            IAnchorStoreFactory anchorStoreFactory,
            ILogger<JobRunner> jobRunnerLogger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _jobChangeTokenProducerFactory = jobChangeTokenProducerFactory;
            _anchorStoreFactory = anchorStoreFactory;
            _jobRunnerLogger = jobRunnerLogger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public JobRunner CreateJobRunner(string jobName, JobConfig config, CancellationToken stoppingToken)
        {
            var anchorStore = _anchorStoreFactory.GetAnchorStore(jobName);
            var changeTokenProducer = _jobChangeTokenProducerFactory.GetChangeTokenProducer(jobName, config, stoppingToken);
            var newJobRunner = new JobRunner(jobName, config, anchorStore, _jobRunnerLogger, _serviceScopeFactory, changeTokenProducer);
            return newJobRunner;
        }
    }
}
