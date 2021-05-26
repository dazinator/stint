namespace Stint.Changify
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    /// <summary>
    /// An <see cref="IChangeTokenProducer"/> that produces change tokens that are signalled when a cron schedule elapses.
    /// The previous trigger time is persisted via an <see cref="IAnchorStore"/>.
    /// </summary>
    public class ScheduledChangeTokenProducer : IChangeTokenProducer
    {
        private readonly Func<CancellationToken, Task<DateTime?>> _getLastRanTime;
        private readonly DelayChangeTokenProducer _innerProducer;

        public ScheduledChangeTokenProducer(
            ILogger logger,
            Func<CancellationToken, Task<DateTime?>> getLastRanTimeAsync,
            Func<DateTime, DateTime?> getNextOccurrence,
            CancellationToken cancellationToken)
        {
            _getLastRanTime = getLastRanTimeAsync;
            var logger1 = logger;
            var cancellationToken1 = cancellationToken;

            _innerProducer = new DelayChangeTokenProducer(async () =>
            {
                var now = DateTime.UtcNow;
                var previousOccurrence = await _getLastRanTime.Invoke(cancellationToken1);

                if (previousOccurrence == null)
                {
                    logger1.LogInformation("Job has not previously run");
                }

                var fromWhenShouldItNextRun =
                    previousOccurrence ?? DateTime.UtcNow; // if we have never run before, get next occurrence from now!

                var occurrence = getNextOccurrence?.Invoke(fromWhenShouldItNextRun);
                //     expression.GetNextOccurrence(fromWhenShouldItNextRun);
                if (occurrence == null)
                {
                    // job won't occur again based on this schedule..
                    // this job is over..
                    return null;
                }

                logger1.LogInformation("Next occurrence {occurrence}", occurrence);
                var difference = occurrence.Value - now;
                return new DelayInfo(difference, cancellationToken1);
            });
        }

        public IChangeToken Produce() => _innerProducer.Produce();
    }
}
