namespace Stint.Triggers.ManualInvoke
{
    using System;

    public interface IJobManualTriggerRegistry
    {
        void AddUpdateTrigger(string jobName, Action trigger);
        bool TryGetTrigger(string jobName, out Action trigger);
    }
}
