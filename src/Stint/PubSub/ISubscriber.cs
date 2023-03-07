namespace Stint.PubSub
{
    using System;

    public interface ISubscriber<TEventArgs>
        where TEventArgs : EventArgs
    {
        IDisposable Subscribe(EventHandler<TEventArgs> handler);
    }

}
