namespace Stint
{
    using System;
    using Dazinator.Extensions.DependencyInjection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    public static class ServiceCollectionExtensions
    {
        public static StintServicesBuilder AddScheduledJobs(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<JobRegistry> registerJobs)
        {
            services.AddHostedService<Worker>();
            services.Configure<SchedulerConfig>(configuration);
            // register job types
            services.AddNamed<IJob>(n => registerJobs?.Invoke(new JobRegistry(n)));

            var builder = new StintServicesBuilder(services);
            builder.AddConfigurationJobOptionsStore(configuration)
                   .AddFileSystemAnchorStore()
                   .AddLockProvider<SingletonLockProvider>();

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
        public StintServicesBuilder AddFileSystemAnchorStore(string basePath)
        {
            Services.AddSingleton<IAnchorStoreFactory, FileSystemAnchorStoreFactory>((sp) => new FileSystemAnchorStoreFactory(basePath));
            return this;
        }

        /// <summary>
        /// Saves anchors to the file system at the <see cref="IHostEnvironment.ContentRootPath"/> location.
        /// </summary>
        /// <param name="basePath"></param>
        /// <returns></returns>
        public StintServicesBuilder AddFileSystemAnchorStore()
        {
            Services.AddSingleton<IAnchorStoreFactory, FileSystemAnchorStoreFactory>((sp) =>
            {
                var env = sp.GetRequiredService<IHostEnvironment>();
                return new FileSystemAnchorStoreFactory(env.ContentRootPath);
            });
            return this;
        }

        public StintServicesBuilder AddConfigurationJobOptionsStore(IConfiguration config, string configSectionFormatString = null)
        {
            Services.AddSingleton<IJobOptionsStore>(new ConfigurationJobOptionsStore(config, configSectionFormatString));
            return this;
        }

        public StintServicesBuilder AddLockProvider<TLockProvider>()
            where TLockProvider : class, ILockProvider
        {
            Services.AddSingleton<ILockProvider, TLockProvider>();
            return this;
        }

        public StintServicesBuilder AddLockProviderInstance(ILockProvider instance)
        {
            Services.AddSingleton<ILockProvider>(instance);
            return this;
        }
    }
}
