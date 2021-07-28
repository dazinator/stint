namespace Stint.PubSub
{
    using System;

    public class MediatorFactory<TEventArgs> : IMediatorFactory<TEventArgs> where TEventArgs : EventArgs
    {
        private readonly Lazy<Mediator<TEventArgs>> _lazyInstance;
        public MediatorFactory() => _lazyInstance = new Lazy<Mediator<TEventArgs>>(() => new Mediator<TEventArgs>());
        public IMediator<TEventArgs> GetMediator() => _lazyInstance.Value;
    }
}
