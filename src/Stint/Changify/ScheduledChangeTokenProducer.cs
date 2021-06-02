namespace Stint.Changify
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Primitives;

    /// <summary>
    /// An <see cref="IChangeTokenProducer"/> that produces <see cref="IChangeToken"/>s that will be signalled at the specified <see cref="DateTime"/>.
    /// </summary>
    public class ScheduledChangeTokenProducer : IChangeTokenProducer
    {
        private readonly DelayChangeTokenProducer _innerProducer;
        public ScheduledChangeTokenProducer(
            Func<Task<DateTime?>> getNextOccurrence,
            CancellationToken cancellationToken)
        {
#pragma warning disable IDE0021 // Use expression body for constructors
            _innerProducer = new DelayChangeTokenProducer(async () =>
            {
                var now = DateTime.UtcNow;
                var occurrence = await getNextOccurrence?.Invoke();
                if (occurrence == null)
                {
                    // change token won't be signalled.                 
                    return null;
                }

                var difference = occurrence.Value - now;
                return new DelayInfo(difference, cancellationToken);
            });
#pragma warning restore IDE0021 // Use expression body for constructors
        }

        public IChangeToken Produce() => _innerProducer.Produce();
    }
}
