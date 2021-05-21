namespace Stint
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.Extensions.Primitives;

    public class Worker : BackgroundService
    {
        private readonly Func<IChangeToken> _changeTokenProducer;
        private readonly IDisposable _changeTokenProducerLifetime;

        private readonly Dictionary<string, ScheduledJobRunner> _jobs = new Dictionary<string, ScheduledJobRunner>();
        private readonly ILogger<Worker> _logger;
        private readonly IOptionsMonitor<SchedulerConfig> _optionsMonitor;
        private readonly IServiceProvider _serviceProvider;
        private readonly IJobSettingsStore _settingsStore;

        private Task _allRunningJobs;

        private CancellationTokenSource _cts;
        private IDisposable _listeningForJobsChanges;
        private TaskCompletionSource<bool> _taskCompletionSource;

        public Worker(
            ILogger<Worker> logger,
            IOptionsMonitor<SchedulerConfig> optionsMonitor,
            IHostEnvironment environment,
            IServiceProvider serviceProvider,
            IJobSettingsStore settingsStore)
        {
            _logger = logger;
            _optionsMonitor = optionsMonitor;
            Environment = environment;
            _serviceProvider = serviceProvider;
            _settingsStore = settingsStore;
            _changeTokenProducer = new ChangeTokenProducerBuilder()
                .IncludeOptionsChangeTrigger(_optionsMonitor)
                .Build(out var producerLifetime);
            _changeTokenProducerLifetime = producerLifetime;
        }

        public IHostEnvironment Environment { get; }

        public void StartListeningForJobConfigChanges() =>
            // reload if tokens signalled.
            _listeningForJobsChanges = ChangeToken.OnChange(_changeTokenProducer, OnJobsChanged);

        public void StopListeningForJobConfigChanges() =>
            // reload if tokens signalled.
            _listeningForJobsChanges?.Dispose();

        private void OnJobsChanged()
        {
            _logger.LogInformation("Jobs config changed at: {time}", DateTimeOffset.Now);
            var latestConfig = _optionsMonitor.CurrentValue;
            _logger.LogInformation("There are now {count} jobs configured.", latestConfig.Jobs?.Count ?? 0);
            ReloadJobs(_cts.Token); // load jobs based on current config.
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Worker executed at: {time}", DateTimeOffset.Now);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _taskCompletionSource = new TaskCompletionSource<bool>();
            ReloadJobs(_cts.Token); // load jobs based on current config.
            StartListeningForJobConfigChanges(); // listen for changes to jobs and apply them.


            // somehow need to await all the jobs to complete.
            // jobs can be added / removed and deleted! :-(
            // maybe we just wait forever by return a task completion source token, that we won't signal - until stoppingToken is cancelled.

            stoppingToken.Register(() =>
            {
                _logger.LogInformation("Worker cancellation signalled at: {time}", DateTimeOffset.Now);
                if (_allRunningJobs.IsCompleted)
                {
                    // no running jobs, so we can signal exit now.
                    _taskCompletionSource.SetResult(true);
                }
            });

            // Note: This _taskCompletionSource is signalled when:
            // 1. The host is cancelled, are there are no running jobs - i.e _allRunningJobs is complete - as shown above.
            // 2. The host is cancelled, there are running jobs, in which case when all of those _allRunningJobs are complete there is a continuation that that signals the task completion source if the host cancellation token is signalled.
            // This mains this tasks runs until the host is cancelled, and if there is no ongoing work, exits quickly, otherwise if there are ongoing tasks, exits after they are all complete.
            await _taskCompletionSource.Task;
            _logger.LogInformation("Worker exiting at: {time}", DateTimeOffset.Now);
        }

        private void ReloadJobs(CancellationToken stoppingToken)
        {
            var currentConfig = _optionsMonitor.CurrentValue;
            var jobsKeys = _jobs.Keys;
            var configKeys = currentConfig?.Jobs?.Select(a => a.Key)?.ToArray() ?? new string[0];

            var jobsToRemove = jobsKeys.Except(configKeys).ToArray();
            _logger.LogInformation("{x} jobs to remove.", jobsToRemove?.Length ?? 0);

            var jobsToAdd = configKeys.Except(jobsKeys).ToArray();
            _logger.LogInformation("{x} jobs to add.", jobsToAdd?.Length ?? 0);

            // yuck, clean this up later.
            // Basically getting all jobs where the options in the config are now actually different that to what they were previously.
            var jobsToUpdate =
                configKeys.Join(jobsKeys, a => a, b => b, (a, b) => a)
                    .ToArray(); // match the keys in config to jobs running now.
            jobsToUpdate = jobsToUpdate.Where(key =>
                !currentConfig?.Jobs.Single(a => a.Key == key).Equals(_jobs[key].JobConfig) ?? false).ToArray();
            _logger.LogInformation("{x} jobs to update.", jobsToUpdate?.Length ?? 0);


            // Note once we have got the new set of tasks we can await it and dispose of the previous await.
            var jobTasks = new List<Task>();

            // throw new NotImplementedException("populate the new tasks list");

            foreach (var key in jobsToAdd)
            {
                var existed = _jobs.TryGetValue(key, out var outJob);
                if (existed)
                {
                    // should probably throw an error here as this shouldn't actually happen.
                    _logger.LogWarning("New job added but it already exists, skipping {name}", key);
                    continue;
                }

                var exists = currentConfig.Jobs.TryGetValue(key, out var jobConfig);
                if (!exists)
                {
                    // should probably throw an error here as this shouldn't actually happen.
                    _logger.LogWarning("New job added but unable to get config, skipping {name}", key);
                    continue;
                }

                //Note: this looks like a resolution root?
                var logger = _serviceProvider.GetRequiredService<ILogger<ScheduledJobRunner>>();
                var newJob = new ScheduledJobRunner(key, jobConfig, GetAnchorStore(key), _settingsStore, logger,
                    _serviceProvider.GetRequiredService<IServiceScopeFactory>());
                var started = newJob.RunAsync(stoppingToken);
                jobTasks.Add(started);
                _jobs.Add(key, newJob);
                _logger.LogInformation("New job started: {name}", key);
            }

            // Update any currently running jobs that have changed.
            foreach (var key in jobsToUpdate)
            {
                var wasRemoved = _jobs.Remove(key, out var existingJob);
                if (!wasRemoved)
                {
                    _logger.LogWarning("Could not remove job named: {name}", key);
                }

                existingJob?.Dispose();
                var exists = currentConfig.Jobs.TryGetValue(key, out var newConfig);
                if (!exists)
                {
                    // should probably throw an error here as this shouldn't actually happen.
                    _logger.LogWarning("Job udpated but unable to get config, skipping {name}", key);
                    continue;
                }

                //Note: this looks like a resolution root?
                var logger = _serviceProvider.GetRequiredService<ILogger<ScheduledJobRunner>>();
                var jobLifetime = new ScheduledJobRunner(key, newConfig, GetAnchorStore(key), _settingsStore, logger,
                    _serviceProvider.GetRequiredService<IServiceScopeFactory>());
                var started = jobLifetime.RunAsync(stoppingToken);
                jobTasks.Add(started);
                _jobs.Add(key, jobLifetime);
                _logger.LogInformation("Job reloaded: {name}", key);
            }

            foreach (var key in jobsToRemove)
            {
                if (!_jobs.Remove(key, out var oldJob))
                {
                    _logger.LogWarning("No running job found named: {name}", key);
                }

                oldJob?.Dispose();
                _logger.LogInformation("Job removed: {name}", key);
            }

            var allJobs = Task.WhenAll(jobTasks).ContinueWith(t =>
            {
                // all jobs have complete because service is terminating
                if (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("All jobs cancelled.");
                    _taskCompletionSource.SetResult(false);
                }
                else
                {
                    _logger.LogInformation("All jobs have finished, waiting for more jobs to be configured.");
                }
            });

            var oldTask = Interlocked.Exchange(ref _allRunningJobs, allJobs);

            // best effort dispose
            // we can't dispose of non completed tasks, so if its completed we will. Otherwise leave it for finaliser.
            // https://stackoverflow.com/questions/5985973/do-i-need-to-dispose-of-a-task
            if (oldTask?.IsCompleted ?? false)
            {
                oldTask.Dispose();
                _logger.LogInformation("Disposed of old completed task.");
            }
        }

        private JobAnchorStore GetAnchorStore(string name) => new JobAnchorStore(Environment.ContentRootPath, name);

        public override void Dispose()
        {
            _logger.LogInformation("Disposing worker.");
            StopJobs();
            _cts?.Dispose();
            _taskCompletionSource?.TrySetResult(false);
            _changeTokenProducerLifetime.Dispose();
            base.Dispose();
        }

        private void StopJobs()
        {
            StopListeningForJobConfigChanges();
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }
    }
}