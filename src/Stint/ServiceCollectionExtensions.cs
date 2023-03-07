namespace Stint
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Stint.PubSub;
    using Stint.Triggers.ManualInvoke;
    using Stint.Triggers.OnCompleted;
    using Stint.Triggers.Schedule;

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddScheduledJobs(
            this IServiceCollection services,
            Action<StintServicesBuilder> configure)
        {
            services.AddHostedService<Worker>();
            var builder = new StintServicesBuilder(services);
            builder.AddFileSystemAnchorStore()
                   .AddLockProvider<EmptyLockProvider>()
                   .AddJobChangeTokenProducerFactory<JobChangeTokenProducerFactory>()
                   .AddJobRunnerFactory<JobRunnerFactory>();

            // Add the default set of trigger providers.
            builder.AddJobCompletionTriggerProvider()
                   .AddManualInvokeTriggerProvider()
                   .AddScheduleTriggerProvider();

            // A simple pub sub implementation for decoupling publshers of events from subscribers.
            services.AddSingleton(typeof(IMediatorFactory<>), typeof(MediatorFactory<>));
            services.AddSingleton(typeof(IPublisher<>), typeof(Publisher<>));
            services.AddSingleton(typeof(ISubscriber<>), typeof(Subscriber<>));


            configure?.Invoke(builder);
            return services;
        }

    }
}
