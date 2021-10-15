namespace Stint
{
    using System;
    using Dazinator.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

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
            Services.AddSingleton<IAnchorStoreFactory, FileSystemAnchorStoreFactory>((sp) =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return new FileSystemAnchorStoreFactory(basePath, loggerFactory);
            });
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
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return new FileSystemAnchorStoreFactory(env.ContentRootPath, loggerFactory);
            });
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

        public StintServicesBuilder AddJobChangeTokenProducerFactory(IJobChangeTokenProducerFactory factoryInstance)
        {
            Services.AddSingleton<IJobChangeTokenProducerFactory>(factoryInstance);
            return this;
        }

        public StintServicesBuilder AddJobChangeTokenProducerFactory<TJobChangeTokenProducerFactory>()
            where TJobChangeTokenProducerFactory : class, IJobChangeTokenProducerFactory
        {
            Services.AddSingleton<IJobChangeTokenProducerFactory, TJobChangeTokenProducerFactory>();
            return this;
        }

        public StintServicesBuilder AddJobRunnerFactory(IJobRunnerFactory factoryInstance)
        {
            Services.AddSingleton<IJobRunnerFactory>(factoryInstance);
            return this;
        }

        public StintServicesBuilder AddJobRunnerFactory<TJobRunnerFactory>()
            where TJobRunnerFactory : class, IJobRunnerFactory
        {
            Services.AddSingleton<IJobRunnerFactory, TJobRunnerFactory>();
            return this;
        }

        public StintServicesBuilder RegisterJobTypes(Action<NamedServiceRegistrationsBuilder<IJob>> registerJobTypes = null)
        {
            var builder = new NamedServiceRegistrationsBuilder<IJob>(Services);
            registerJobTypes?.Invoke(builder);
            return this;
        }
    }
}
