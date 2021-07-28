namespace Stint
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Stint.PubSub;

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

            // jobs that can be manually triggered have a trigger callback added to the registry, looked up by job name.
            // the IJobManualTriggerInvoker can be injected and used to trigger any of these jobs, using the job name as an argument.
            services.AddSingleton<IJobManualTriggerRegistry, JobManualTriggerRegistry>();
            services.AddSingleton<IJobManualTriggerInvoker, JobManualTriggerInvoker>();

            // A simple pub sub implementation for decoupling publshers of events from subscribers.
            services.AddSingleton(typeof(IMediatorFactory<>), typeof(MediatorFactory<>));
            services.AddSingleton(typeof(IPublisher<>), typeof(Publisher<>));
            services.AddSingleton(typeof(ISubscriber<>), typeof(Subscriber<>));

            configure?.Invoke(builder);
            return services;
        }

    }
}
