namespace Stint
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IJob
    {
        Task ExecuteAsync(ExecutionInfo runInfo, CancellationToken token);
    }
}
