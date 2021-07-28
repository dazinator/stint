namespace Stint
{
    using System.Threading;
    using Microsoft.Extensions.Primitives;

    public interface IJobChangeTokenProducerFactory
    {
        IChangeTokenProducer GetChangeTokenProducer(string jobName, JobConfig jobConfig, CancellationToken cancellationToken);
    }
}
