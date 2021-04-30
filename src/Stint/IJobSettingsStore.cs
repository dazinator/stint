namespace Stint
{
    public interface IJobSettingsStore
    {
        TOptions GetOptions<TOptions>(string name) where TOptions : new();
    }
}
