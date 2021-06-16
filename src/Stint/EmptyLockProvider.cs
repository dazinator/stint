namespace Stint
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// A lock provider that always works returning an empty lock - essentially its meaningless as its not locking anything.
    /// </summary>
    public class EmptyLockProvider : ILockProvider
    {
        private readonly Task<IDisposable> _emptyLock = Task.FromResult<IDisposable>(EmptyDisposable.Instance);

        public EmptyLockProvider()
        {
        }

        public Task<IDisposable> TryAcquireAsync(string name) => _emptyLock;
    }
}
