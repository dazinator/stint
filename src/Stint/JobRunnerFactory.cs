namespace Stint
{
    using System.Threading;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using PubSub;

    public class JobRunnerFactory : IJobRunnerFactory
    {
        private readonly ILogger<JobRunnerFactory> _logger;
        private readonly IJobChangeTokenProducerFactory _jobChangeTokenProducerFactory;
        private readonly IAnchorStoreFactory _anchorStoreFactory;
        private readonly ILogger<JobRunner> _jobRunnerLogger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IPublisher<JobCompletedEventArgs> _publisher;
        private readonly ILockProvider _lockProvider;
        private readonly IOptions<ActivityOptions> _activityOptions;

        public JobRunnerFactory(
            ILogger<JobRunnerFactory> logger,
            IJobChangeTokenProducerFactory jobChangeTokenProducerFactory,
            IAnchorStoreFactory anchorStoreFactory,
            ILogger<JobRunner> jobRunnerLogger,
            IServiceScopeFactory serviceScopeFactory,
            IPublisher<JobCompletedEventArgs> publisher,
            ILockProvider lockProvider,
            IOptions<ActivityOptions> activityOptions)
        {
            _logger = logger;
            _jobChangeTokenProducerFactory = jobChangeTokenProducerFactory;
            _anchorStoreFactory = anchorStoreFactory;
            _jobRunnerLogger = jobRunnerLogger;
            _serviceScopeFactory = serviceScopeFactory;
            _publisher = publisher;
            this._lockProvider = lockProvider;
            _activityOptions = activityOptions;
        }

        public IJobRunner CreateJobRunner(string jobName, JobConfig config, CancellationToken stoppingToken)
        {
            _logger.LogDebug("Creating job runner for job {jobName}", jobName);
            var anchorStore = _anchorStoreFactory.GetAnchorStore(jobName);
            var changeTokenProducer = _jobChangeTokenProducerFactory.GetChangeTokenProducer(jobName, config, stoppingToken);
            var newJobRunner = new JobRunner(jobName, _lockProvider, config, anchorStore, _jobRunnerLogger, _serviceScopeFactory, changeTokenProducer, _publisher, _activityOptions.Value);
            return newJobRunner;
        }
    }
}
