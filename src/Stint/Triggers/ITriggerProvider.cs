namespace Stint.Triggers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Primitives;

    public interface ITriggerProvider
    {
        void AddTriggerChangeTokens(
            string jobName,
            JobConfig jobConfig,
            Func<Task<DateTime?>> lastRanAnchorTaskFactory,
            ChangeTokenProducerBuilder builder,
            CancellationToken cancellationToken);
    }
}
