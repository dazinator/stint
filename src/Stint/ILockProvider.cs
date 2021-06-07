namespace Stint
{
    using System;
    using System.Threading.Tasks;

    public interface ILockProvider
    {
        Task<IDisposable> TryAcquireAsync(string name);
    }
}
