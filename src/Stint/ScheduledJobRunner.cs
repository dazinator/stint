namespace Stint
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;

    public class ScheduledJobRunner : IDisposable
    {
        private readonly IAnchorStore _anchorStore;
        private readonly ILogger<ScheduledJobRunner> _logger;
        private readonly IJobOptionsStore _optionsStore;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IChangeTokenProducer _changeTokenProducer;

        public ScheduledJobRunner(
                string name,
                ScheduledJobConfig config,
                IAnchorStore anchorStore,
                IJobOptionsStore optionsStore,
                ILogger<ScheduledJobRunner> logger,
                IServiceScopeFactory serviceScopeFactory,
                IChangeTokenProducer changeTokenProducer
            )
        {
            Name = name;
            Config = config;
            _anchorStore = anchorStore;
            _optionsStore = optionsStore;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _changeTokenProducer = changeTokenProducer;
        }

        public CancellationTokenSource CancellationTokenSource { get; set; }
        public string Name { get; }
        public ScheduledJobConfig Config { get; }
        public void Dispose()
        {
            CancellationTokenSource?.Cancel();
            CancellationTokenSource?.Dispose();
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            // consider
            // Subscribing to a change token producer, signalled by a scheduled trigger.

            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return RunToScheduleAsync(CancellationTokenSource.Token);
        }

        public async Task RunToScheduleAsync(CancellationToken token)
        {
            // DateTime? previousOccurrence = null;         

            while (!token.IsCancellationRequested)
            {
                await _changeTokenProducer.WaitOneAsync(); // wait for a change token to be signalled.
                if (token.IsCancellationRequested)
                {
                    continue;
                }

                // run now!
                var jobInfo = new ExecutionInfo(Name, _optionsStore);

                // TODO: Add options for retrying when failure.
                await ExecuteScheduledJob(Config.Type, jobInfo, token);
                var newAnchor = await _anchorStore.DropAnchorAsync(token);
            }

            _logger.LogInformation("Job cancelled");
        }


        protected virtual async Task ExecuteScheduledJob(string jobTypeName, ExecutionInfo runInfo, CancellationToken token)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var factory = scope.ServiceProvider.GetRequiredService<Func<string, IJob>>();

                // Do all the work we need to do!
                IJob job;
                try

                {
                    job = factory.Invoke(jobTypeName);
                    if (job == null)
                    {
                        // no such job registered..
                        _logger.LogWarning("No job type named {name} is registered.", jobTypeName);
                        return;
                    }
                }
                catch (KeyNotFoundException)
                {
                    // unable to find job type specified..
                    _logger.LogWarning("No job type named {name} is registered.", jobTypeName);
                    return;
                    // throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to create job type named {name} - it might be missing dependencies.",
                        jobTypeName);
                    return;
                }

                try
                {
                    await job.ExecuteAsync(runInfo, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Job error.");
                    // don't allow job execution exceptions to bubble any further.
                    // return success and log error.
                    // jobs must currently handle their own retry logic..
                }
            }
        }
    }
}
