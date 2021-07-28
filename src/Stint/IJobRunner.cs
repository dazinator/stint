namespace Stint
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IJobRunner : IDisposable
    {
        JobConfig Config { get; }
        Task RunAsync(CancellationToken cancellationToken);
    }
}
