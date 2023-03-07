namespace Stint.Triggers.ManualInvoke
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Stint;
    using Stint.Triggers;

    public class ManualInvokeTriggerProvider : ITriggerProvider
    {
        private readonly ILogger<ManualInvokeTriggerProvider> _logger;
        private readonly IJobManualTriggerRegistry _triggers;

        public ManualInvokeTriggerProvider(ILogger<ManualInvokeTriggerProvider> logger,
            IJobManualTriggerRegistry triggers)
        {
            _logger = logger;
            _triggers = triggers;
        }

        public void AddTriggerChangeTokens(string jobName,
            JobConfig jobConfig,
            Func<Task<DateTime?>> lastRanAnchorTaskFactory,
            ChangeTokenProducerBuilder builder,
            CancellationToken cancellationToken)
        {

            if (jobConfig?.Triggers?.Manual ?? false)
            {
                // register a delegate that can trigger this job, by the job name.
                builder.IncludeTrigger(out var triggerDelegate);
                _triggers.AddUpdateTrigger(jobName, triggerDelegate);
            }
        }
    }
}
