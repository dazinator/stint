namespace Stint.PubSub
{
    using System;
    using Stint.Utils;

    public class Subscriber<TEventArgs> : ISubscriber<TEventArgs>
        where TEventArgs : EventArgs
    {
        private readonly IMediator<TEventArgs> _mediator;

        public Subscriber(IMediatorFactory<TEventArgs> mediatorFatory) => _mediator = mediatorFatory.GetMediator();

        public IDisposable Subscribe(EventHandler<TEventArgs> handler)
        {
            _mediator.OnEvent += handler;
            return new ActionOnDispose(() => _mediator.OnEvent -= handler);
        }
    }
}
