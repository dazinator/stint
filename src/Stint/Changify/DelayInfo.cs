namespace Stint.Changify
{
    using System;
    using System.Threading;

    public class DelayInfo
    {
        public DelayInfo(TimeSpan delayFor, CancellationToken delayCancellationToken)
        {
            DelayFor = delayFor;
            DelayCancellationToken = delayCancellationToken;
        }

        public CancellationToken DelayCancellationToken { get; set; }
        public TimeSpan DelayFor { get; set; }
    }
}
