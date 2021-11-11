namespace Stint
{
    using System;
    using System.Collections.Concurrent;

    public class JobManualTriggerRegistry : IJobManualTriggerRegistry
    {
        private readonly ConcurrentDictionary<string, Action> _jobTriggerDelegates = new ConcurrentDictionary<string, Action>();

        public bool TryGetTrigger(string jobName, out Action trigger) => _jobTriggerDelegates.TryGetValue(jobName, out trigger);

        public void AddUpdateTrigger(string jobName, Action trigger) => _ = _jobTriggerDelegates.AddOrUpdate(jobName, trigger, (key, oldValue) => trigger);

    }
}
