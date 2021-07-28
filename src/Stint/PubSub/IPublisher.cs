namespace Stint.PubSub
{
    using System;

    public interface IPublisher<TEventArgs>
        where TEventArgs : EventArgs
    {
        void Publish(object sender, TEventArgs args);
    }

}
