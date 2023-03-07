namespace Stint.Triggers.OnCompleted
{
    using Dazinator.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection;
    using Stint.Triggers;

    public static class JobCompletionTriggerExtensions
    {
        public static StintServicesBuilder AddJobCompletionTriggerProvider(this StintServicesBuilder builder)
        {
            builder.Services.AddScoped<ITriggerProvider, JobCompletionTriggerProvider>();
            return builder;
        }
    }
}
