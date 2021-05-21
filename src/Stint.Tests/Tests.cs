namespace Stint.Tests
{
    using System;
    using System.Collections.Generic;
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

            var hostBuilderTask = CreateHostBuilder(() =>
                {
                    jobRanEvent.Set();
                }
            ).Build().RunAsync();

            var signalled = jobRanEvent.WaitOne(60000);
            Assert.True(signalled);
        }

        public static IHostBuilder CreateHostBuilder(Action onJobExecuted)
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
                {
                    services.AddHostedService<Worker>();
                    services.Configure<SchedulerConfig>(jobsConfigSection);
                    services.AddScheduledJobs(jobsConfigSection,
                        (r) =>
                        {
                            r.Include<TestJob>(nameof(TestJob), sp => new TestJob(onJobExecuted));
                        });
                });
        }

        public class TestJob : IJob
        {
            private readonly Action _onJobExecuted;

            public TestJob(Action onJobExecuted)
            {
                _onJobExecuted = onJobExecuted;
            }

            public Task ExecuteAsync(ExecutionInfo runInfo, CancellationToken token)
            {
                _onJobExecuted();
                return Task.CompletedTask;
            }
        }
    }
}
