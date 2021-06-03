namespace Stint
{
    using System;
    using Dazinator.Extensions.DependencyInjection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    public static class ServiceCollectionExtensions
    {
        public static StintServicesBuilder AddScheduledJobs(this IServiceCollection services,
            IConfiguration configuration,
            Action<JobRegistry> registerJobs)
        {
            services.AddHostedService<Worker>();
            // register job types
            services.AddNamed<IJob>(n =>
            {
                registerJobs?.Invoke(new JobRegistry(configuration, services, n));
            });

            services.AddSingleton<IJobOptionsStore>(new ConfigurationJobOptionsStore(configuration));
            services.AddSingleton<IAnchorStoreFactory, FileSystemAnchorStoreFactory>((sp) =>
            {
                var env = sp.GetRequiredService<IHostEnvironment>();
                return new FileSystemAnchorStoreFactory(env.ContentRootPath);
            });

            return new StintServicesBuilder(services);
        }
    }

    public class StintServicesBuilder
    {
        public StintServicesBuilder(IServiceCollection services) => Services = services;

        public IServiceCollection Services { get; }

        /// <summary>
        /// Saves anchors to the file system at the specified location.
        /// </summary>
        /// <param name="basePath"></param>
        /// <returns></returns>
        public StintServicesBuilder UseFileSystemAnchorStore(string basePath)
        {
            Services.AddSingleton<IAnchorStoreFactory, FileSystemAnchorStoreFactory>((sp) => new FileSystemAnchorStoreFactory(basePath));
            return this;
        }

        public StintServicesBuilder UseConfigurationJobOptionsStore(IConfiguration config, string configSectionFormatString = null)
        {
            Services.AddSingleton<IJobOptionsStore>(new ConfigurationJobOptionsStore(config, configSectionFormatString));
            return this;
        }
    }
}
