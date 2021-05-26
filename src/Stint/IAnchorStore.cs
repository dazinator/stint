namespace Stint
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IAnchorStore
    {
        Task<DateTime?> GetAnchorAsync(CancellationToken token);
        Task<DateTime> DropAnchorAsync(CancellationToken token);
    }
}
