namespace Stint.Triggers.ManualInvoke
{
    public interface IJobManualTriggerInvoker
    {
        bool Trigger(string jobName);
    }
}
