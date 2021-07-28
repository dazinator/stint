namespace Stint.Utils
{
    using System;

    public class ActionOnDispose : IDisposable
    {

        public ActionOnDispose(Action onDispose) => _onDispose = onDispose;

        private bool _disposedValue;
        private readonly Action _onDispose;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _onDispose?.Invoke();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

}
