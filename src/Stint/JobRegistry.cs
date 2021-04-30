namespace Stint
{
    using System;
    using Dazinator.Extensions.DependencyInjection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public class JobRegistry
    {
        private readonly IConfiguration _config;
        private readonly IServiceCollection _services;

        //   public Dictionary<string, Type> _jobOptionsTypes { get; set; } = new Dictionary<string, Type>();

        public JobRegistry(
            IConfiguration config,
            IServiceCollection services,
            NamedServiceRegistry<IJob> namedServiceRegistry)
        {
            _config = config;
            _services = services;
            NamedServiceRegistry = namedServiceRegistry;
        }

        private NamedServiceRegistry<IJob> NamedServiceRegistry { get; }

        public JobRegistry Include<TJob>(string name)
            where TJob : IJob
        {
            NamedServiceRegistry.AddTransient<TJob>(name);
            return this;
        }

        public JobRegistry Include<TJob>(string name, Func<IServiceProvider, TJob> factory)
            where TJob : IJob
        {
            NamedServiceRegistry.AddTransient(name, factory);
            return this;
        }

        //     public void ConfigureOptionsTypes()
        //     {
        //         foreach (var item in _jobOptionsTypes)
        //         {
        //             var configPath = $"{item.Key}:Settings";

        //             // _services.Configure<OptionsType>("name", _config);
        //             object? result = typeof(OptionsServiceCollectionExtensions)
        // .GetMethod(nameof(OptionsServiceCollectionExtensions.Configure))
        // .MakeGenericMethod(item.Value)
        // .Invoke(null, new object[] { _services, item.Key, _config });

        //         }
        //     }
    }
}
