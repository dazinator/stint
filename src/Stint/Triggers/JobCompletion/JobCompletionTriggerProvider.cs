namespace Stint.Triggers.OnCompleted
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Stint;
    using Stint.PubSub;
    using Stint.Triggers;

    public class JobCompletionTriggerProvider : ITriggerProvider
    {
        private readonly ILogger<JobCompletionTriggerProvider> _logger;
        private readonly ISubscriber<JobCompletedEventArgs> _subscriber;


        public JobCompletionTriggerProvider(ILogger<JobCompletionTriggerProvider> logger,
             ISubscriber<JobCompletedEventArgs> subscriber)
        {
            _logger = logger;
            _subscriber = subscriber;
        }

        public void AddTriggerChangeTokens(
          string jobName,
          JobConfig jobConfig,
          Func<Task<DateTime?>> lastRanAnchorTaskFactory,
          ChangeTokenProducerBuilder builder,
          CancellationToken cancellationToken)
        {
            // If job is configured to run when other jobs complete,
            // then subscribe to job completion events, and when an event is received, if the job name that completed
            // matches the job name that should trigger this job, then invoke the trigger for this job!
            var onJobCompletedTriggers = jobConfig.Triggers?.JobCompletions;
            if (onJobCompletedTriggers?.Any() ?? false)
            {
                builder.IncludeSubscribingHandlerTrigger((trigger) => _subscriber.Subscribe((s, e) =>
                {
                    foreach (var jobCompletedTrigger in onJobCompletedTriggers)
                    {
                        if (string.Equals(jobCompletedTrigger.JobName, e.Name))
                        {
                            trigger?.Invoke();
                            break;
                        }
                    }
                }));
            }
        }
    }
}
