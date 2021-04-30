namespace Scheduler
{
    using System;
    using Dazinator.Extensions.DependencyInjection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddScheduledJobs(this IServiceCollection services,
            IConfiguration configuration,
            Action<JobRegistry> registerJobs)
        {
            services.AddHostedService<Worker>();
            // register job types
            services.AddNamed<IJob>(n =>
            {
                registerJobs?.Invoke(new JobRegistry(configuration, services, n));
            });

            services.AddSingleton<IJobSettingsStore>(new ConfigurationJobSettingsStore(configuration));

            return services;
        }
    }
}
