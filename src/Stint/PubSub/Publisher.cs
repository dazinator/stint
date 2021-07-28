namespace Stint.PubSub
{
    using System;

    public class Publisher<TEventArgs> : IPublisher<TEventArgs>
         where TEventArgs : EventArgs
    {
        private readonly IMediator<TEventArgs> _mediator;

        public Publisher(IMediatorFactory<TEventArgs> mediatorFatory) => _mediator = mediatorFatory.GetMediator();
        public void Publish(object sender, TEventArgs args) => _mediator.Raise(sender, args);

    }

}
