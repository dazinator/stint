namespace Stint.Triggers.Schedule
{
    using Microsoft.Extensions.DependencyInjection;
    using Stint.Triggers;

    public static class ScheduleTriggerExtensions
    {
        public static StintServicesBuilder AddScheduleTriggerProvider(this StintServicesBuilder builder)
        {
            builder.Services.AddScoped<ITriggerProvider, ScheduleTriggerProvider>();
            return builder;
        }
    }
}
