namespace Stint
{
    using System;
    using Microsoft.Extensions.DependencyInjection;

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddScheduledJobs(
            this IServiceCollection services,
            Action<StintServicesBuilder> configure)
        {
            services.AddHostedService<Worker>();
            var builder = new StintServicesBuilder(services);
            builder.AddFileSystemAnchorStore()
                   .AddLockProvider<EmptyLockProvider>();

            configure?.Invoke(builder);
            return services;
        }

    }
}
