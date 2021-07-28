namespace Stint
{
    public interface IJobManualTriggerInvoker
    {
        bool Trigger(string jobName);
    }
}
