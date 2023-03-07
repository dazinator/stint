namespace Stint.Triggers.ManualInvoke
{
    using Microsoft.Extensions.Logging;

    public class JobManualTriggerInvoker : IJobManualTriggerInvoker
    {
        private readonly ILogger<JobManualTriggerInvoker> _logger;
        private readonly IJobManualTriggerRegistry _registry;

        public JobManualTriggerInvoker(ILogger<JobManualTriggerInvoker> logger, IJobManualTriggerRegistry registry)
        {
            _logger = logger;
            _registry = registry;
        }
        public bool Trigger(string jobName)
        {
            _logger.LogInformation("Invoking manual trigger for {jobname}", jobName);
            if (_registry.TryGetTrigger(jobName, out var trigger))
            {
                trigger?.Invoke();
                return true;
            }

            _logger.LogWarning("Manual trigger not found for job {jobname}. Job wasn't triggered.", jobName);
            return false;
        }
    }
}
