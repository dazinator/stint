namespace Stint.Triggers
{
    using System.Threading;
    using Microsoft.Extensions.Primitives;

    public interface ITriggerProvider
    {
        void AddTriggerChangeTokens(
            string jobName,
            JobConfig jobConfig,
            ChangeTokenProducerBuilder builder,
            CancellationToken cancellationToken);
    }
}
