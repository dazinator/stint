namespace Stint.Changify
{
    using System;
    using System.Threading;

    /// <summary>
    /// Information about a specific delay that will be used to signal an <see cref="Microsoft.Extensions.Primitives.IChangeToken"/>
    /// </summary>
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
