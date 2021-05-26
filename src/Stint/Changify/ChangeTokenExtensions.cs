namespace Stint.Changify
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Primitives;

    public static class ChangeTokenExtensions
    {
        public static Task DelayUntilChangeSignalledAsync(this Func<IChangeToken> changeTokenProducer)
        {
            if (changeTokenProducer == null)
            {
                throw new ArgumentNullException(nameof(ChangeTokenExtensions));
            }
            // consume token, and when signalled complete task completion source..
            var tcs = new TaskCompletionSource<bool>();

            var token = changeTokenProducer.Invoke();
            var handlerLifetime = token.RegisterChangeCallback((state) =>
            {
                tcs.SetResult(true);
            }, null);

            return tcs.Task.ContinueWith(a => handlerLifetime.Dispose());
        }
    }
}
