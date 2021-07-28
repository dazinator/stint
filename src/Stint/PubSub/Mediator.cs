namespace Stint.PubSub
{
    using System;

    public class Mediator<TEventArgs> : IMediator<TEventArgs> where TEventArgs : EventArgs
    {
        public event EventHandler<TEventArgs> OnEvent = delegate { };

        public void Raise(object sender, TEventArgs args) => OnEvent(sender, args);
    }

}
