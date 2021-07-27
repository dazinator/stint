namespace Stint
{
    using System.Threading;

    public interface IJobRunnerFactory
    {
        JobRunner CreateJobRunner(string jobName, JobConfig config, CancellationToken stoppingToken);
    }
}
