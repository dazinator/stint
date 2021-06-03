namespace Stint
{
    using Microsoft.Extensions.Configuration;

    public class ConfigurationJobOptionsStore : IJobOptionsStore
    {
        public const string DefaultConfigSectionPathFormatString = "Jobs:{0}:Settings";
        private readonly IConfiguration _config;
        private readonly string _configSectionPathFormatString;

        public ConfigurationJobOptionsStore(IConfiguration config, string sectionPathFormatString = DefaultConfigSectionPathFormatString)
        {
            _config = config;
            _configSectionPathFormatString = sectionPathFormatString;
        }

        public TOptions GetOptions<TOptions>(string name)
            where TOptions : new()
        {
            var configPath = string.Format(_configSectionPathFormatString, name);
            var section = _config.GetSection(configPath);
            var options = new TOptions();
            section.Bind(options);
            return options;
        }
    }
}
