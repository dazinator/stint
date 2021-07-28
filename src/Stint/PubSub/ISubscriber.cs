using System;

namespace Stint.PubSub
{
    public interface ISubscriber<TEventArgs>
        where TEventArgs : EventArgs
    {
        IDisposable Subscribe(EventHandler<TEventArgs> handler);
    }

}
