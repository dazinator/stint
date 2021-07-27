using System.Threading;
using Microsoft.Extensions.Primitives;

namespace Stint
{
    public interface IJobChangeTokenProducerFactory
    {
        IChangeTokenProducer GetChangeTokenProducer(string jobName, JobConfig jobConfig, CancellationToken cancellationToken);
    }
}