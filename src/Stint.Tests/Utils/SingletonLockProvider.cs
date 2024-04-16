namespace Stint
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A lock provider that only allows one job to run at a time, irrespective of the type / name of the job.
    /// </summary>
    public class SingletonLockProvider : ILockProvider
    {
        private IDisposable _acquiredLock = null;
        private readonly object _lock = new object();
        private readonly Task<IDisposable> _nullLock = Task.FromResult<IDisposable>(null);

        public SingletonLockProvider()
        {
        }

        public Task<IDisposable> TryAcquireAsync(string name, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (_acquiredLock != null)
                {
                    // If the lock is already taken, return a null lock indicating the acquisition failed.
                    return _nullLock;
                }

                // Acquire the lock by setting _acquiredLock to a new disposable that will release the lock.
                _acquiredLock = new InvokeOnDispose(() => ReleaseLock());
                return Task.FromResult(_acquiredLock);
            }
        }

        private void ReleaseLock()
        {
            lock (_lock)
            {
                _acquiredLock = null;
            }
        }
    }
}
