using System;

namespace Stint.PubSub
{
    public interface IMediator<TEventArgs> where TEventArgs : EventArgs
    {
        event EventHandler<TEventArgs> OnEvent;

        void Raise(object sender, TEventArgs args);
    }
}
