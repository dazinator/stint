namespace Stint
{
    using System.Threading;

    public interface IJobRunnerFactory
    {
        IJobRunner CreateJobRunner(string jobName, JobConfig config, CancellationToken stoppingToken);
    }
}
