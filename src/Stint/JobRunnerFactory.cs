namespace Stint
{
    using System.Threading;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Stint.PubSub;

    public class JobRunnerFactory : IJobRunnerFactory
    {
        private readonly IJobChangeTokenProducerFactory _jobChangeTokenProducerFactory;
        private readonly IAnchorStoreFactory _anchorStoreFactory;
        private readonly ILogger<JobRunner> _jobRunnerLogger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IPublisher<JobCompletedEventArgs> _publisher;

        public JobRunnerFactory(
            IJobChangeTokenProducerFactory jobChangeTokenProducerFactory,
            IAnchorStoreFactory anchorStoreFactory,
            ILogger<JobRunner> jobRunnerLogger,
            IServiceScopeFactory serviceScopeFactory,
            IPublisher<JobCompletedEventArgs> publisher)
        {
            _jobChangeTokenProducerFactory = jobChangeTokenProducerFactory;
            _anchorStoreFactory = anchorStoreFactory;
            _jobRunnerLogger = jobRunnerLogger;
            _serviceScopeFactory = serviceScopeFactory;
            _publisher = publisher;
        }

        public IJobRunner CreateJobRunner(string jobName, JobConfig config, CancellationToken stoppingToken)
        {
            var anchorStore = _anchorStoreFactory.GetAnchorStore(jobName);
            var changeTokenProducer = _jobChangeTokenProducerFactory.GetChangeTokenProducer(jobName, config, stoppingToken);
            var newJobRunner = new JobRunner(jobName, config, anchorStore, _jobRunnerLogger, _serviceScopeFactory, changeTokenProducer, _publisher);
            return newJobRunner;
        }
    }
}
