namespace Stint.Triggers.JobCompletion
{
    using Microsoft.Extensions.DependencyInjection;
    using Stint.Triggers;

    public static class JobCompletionTriggerExtensions
    {
        public static StintServicesBuilder AddJobCompletionTriggerProvider(this StintServicesBuilder builder)
        {
            builder.Services.AddSingleton<ITriggerProvider, JobCompletionTriggerProvider>();
            return builder;
        }
    }
}
