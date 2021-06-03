namespace Stint
{
    public interface IJobOptionsStore
    {
        TOptions GetOptions<TOptions>(string name) where TOptions : new();
    }
}
