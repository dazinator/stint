namespace Stint.Triggers.ManualInvoke
{
    using Microsoft.Extensions.DependencyInjection;
    using Stint.Triggers;

    public static class JobManualTriggerExtensions
    {
        public static StintServicesBuilder AddManualInvokeTriggerProvider(this StintServicesBuilder builder)
        {
            builder.Services.AddScoped<ITriggerProvider, ManualInvokeTriggerProvider>();

            // jobs that can be manually triggered have a trigger callback added to the registry, looked up by job name.
            // the IJobManualTriggerInvoker can then be injected and used to trigger any of these jobs, using the job name as an argument.
            builder.Services.AddSingleton<IJobManualTriggerRegistry, JobManualTriggerRegistry>();
            builder.Services.AddSingleton<IJobManualTriggerInvoker, JobManualTriggerInvoker>();

            return builder;
        }
    }
}
