namespace Stint.Changify
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Primitives;

    public static class ChangeTokenExtensions
    {
        /// <summary>
        /// Consumes a single <see cref="IChangeToken"/> from the producer, and asynchronously waits for it to be signalled.
        /// </summary>
        /// <param name="changeTokenProducer"></param>
        /// <returns></returns>
        public static Task WaitAsync(this Func<IChangeToken> changeTokenProducer)
        {
            if (changeTokenProducer == null)
            {
                throw new ArgumentNullException(nameof(changeTokenProducer));
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
