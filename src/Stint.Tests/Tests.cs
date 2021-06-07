namespace Stint.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Xunit;

    public class StintTests
    {
        [Fact]
        public void Can_Run_Scheduled_Job()
        {

            var jobRanEvent = new AutoResetEvent(false);

            var hostBuilderTask = CreateHostBuilder(async () =>
                {
                    jobRanEvent.Set();
                }, new SingletonLockProvider()
            ).Build().RunAsync();

            var signalled = jobRanEvent.WaitOne(60000);
            Assert.True(signalled);
        }

        [Fact]
        public async Task Only_One_Instance_Of_Scheduled_Job_Executed_Concurrently()
        {
            int hostCount = 3;
            var jobRanEvent = new CountdownEvent(hostCount);
            var hosts = new List<IHost>();
            var lockProvider = new SingletonLockProvider();

            for (int i = 0; i < hostCount; i++)
            {
                var host = CreateHostBuilder(async () =>
                {
                    await Task.Delay(1000);
                    jobRanEvent.Signal();
                }, lockProvider).Build();
                hosts.Add(host);
            }

            var tasks = hosts.Select(a => a.StartAsync());
            await Task.WhenAll(tasks);

            await Task.Delay(4000);
            Assert.Equal(2, jobRanEvent.CurrentCount);
        }

        public static IHostBuilder CreateHostBuilder(Func<Task> onJobExecuted, ILockProvider lockProvider)
        {

            var inMemoryConfig = new Dictionary<string, string>();
            // inMemoryConfig.Add("Scheduler", "");
            inMemoryConfig.Add($"Scheduler:Jobs:{nameof(TestJob)}:Schedule", "* * * * *");
            inMemoryConfig.Add($"Scheduler:Jobs:{nameof(TestJob)}:Type", nameof(TestJob));

            // Scheduler
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddInMemoryCollection(inMemoryConfig)
                //.AddJsonFile("appsettings.json", optional: true)
                .Build();

            const string JobsConfigSectionName = "Scheduler";
            var jobsConfigSection = config.GetSection(JobsConfigSectionName);


            return Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                    services.AddScheduledJobs(jobsConfigSection,
                        (r) => r.Include<TestJob>(nameof(TestJob), sp => new TestJob(onJobExecuted)))
                            .AddLockProviderInstance(lockProvider));
        }

        public class TestJob : IJob
        {
            private readonly Func<Task> _onJobExecuted;

            public TestJob(Func<Task> onJobExecuted)
            {
                _onJobExecuted = onJobExecuted;
            }

            public async Task ExecuteAsync(ExecutionInfo runInfo, CancellationToken token) => await _onJobExecuted();
        }
    }
}
