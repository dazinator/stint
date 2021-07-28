namespace Stint
{
    public class JobManualTriggerInvoker : IJobManualTriggerInvoker
    {
        private readonly IJobManualTriggerRegistry _registry;

        public JobManualTriggerInvoker(IJobManualTriggerRegistry registry) => _registry = registry;
        public bool Trigger(string jobName)
        {
            if (_registry.TryGetTrigger(jobName, out var trigger))
            {
                trigger?.Invoke();
                return true;
            }
            return false;
        }
    }
}
