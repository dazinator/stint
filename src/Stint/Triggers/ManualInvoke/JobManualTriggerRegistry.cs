namespace Stint.Triggers.ManualInvoke
{
    using System;
    using System.Collections.Concurrent;

    public class JobManualTriggerRegistry : IJobManualTriggerRegistry
    {
        private readonly ConcurrentDictionary<string, Action> _jobTriggerDelegates = new ConcurrentDictionary<string, Action>();

        public bool TryGetTrigger(string jobName, out Action trigger)
        {
            var result = _jobTriggerDelegates.TryGetValue(jobName, out trigger);
            return result;
        }

        public void AddUpdateTrigger(string jobName, Action trigger)
        {
            var result = _jobTriggerDelegates.AddOrUpdate(jobName, trigger, (key, oldValue) => trigger);
            return;
        }

    }
}
