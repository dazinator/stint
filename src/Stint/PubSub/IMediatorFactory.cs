namespace Stint.PubSub
{
    using System;

    public interface IMediatorFactory<TEventArgs> where TEventArgs : EventArgs
    {
        IMediator<TEventArgs> GetMediator();
    }
}
