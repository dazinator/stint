namespace Stint.Changify
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Primitives;

    public class DelayChangeTokenProducer : IChangeTokenProducer
    {
        private readonly Func<Task<DelayInfo>> _getNextDelayInfo;

        public DelayChangeTokenProducer(Func<Task<DelayInfo>> getNextDelayInfo)
        {
            _getNextDelayInfo = getNextDelayInfo;
        }

        public IChangeToken Produce()
        {
            var delay = _getNextDelayInfo?.Invoke();
            if (delay == null)
            {
                return EmptyChangeToken.Instance;
            }

            var token = new TriggerChangeToken();

            _ = Task.Run(async () =>
            {
                var delay = await _getNextDelayInfo();
                var totalMs = (long)delay.DelayFor.TotalMilliseconds;
                await LongDelay.For(totalMs, delay.DelayCancellationToken);
                if (!delay.DelayCancellationToken.IsCancellationRequested)
                {
                    token.Trigger();
                }
            });

            return token;
        }
    }
}
