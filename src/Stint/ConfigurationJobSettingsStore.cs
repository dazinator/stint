namespace Stint
{
    using Microsoft.Extensions.Configuration;

    public class ConfigurationJobSettingsStore : IJobSettingsStore
    {
        private readonly IConfiguration _config;

        public ConfigurationJobSettingsStore(IConfiguration config) => _config = config;

        public TOptions GetOptions<TOptions>(string name)
            where TOptions : new()
        {
            var configPath = $"Jobs:{name}:Settings";
            var section = _config.GetSection(configPath);
            var options = new TOptions();
            section.Bind(options);
            return options;
        }
    }
}
