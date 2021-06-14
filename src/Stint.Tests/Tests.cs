namespace Stint.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Dazinator.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Xunit;

    public partial class StintTests
    {
        [Fact]
        public void Can_Run_Scheduled_Job()
        {

            var jobRanEvent = new AutoResetEvent(false);


            // services.Configure<SchedulerConfig>(configuration);
            //   a => a.AddTransient(nameof(TestJob), (sp) => new TestJob(onJobExecuted))

            var hostBuilderTask = CreateHostBuilder(new SingletonLockProvider(), (scheduler) =>
            {

                scheduler.Jobs.Add("TestJob", new ScheduledJobConfig() { Schedule = "* * * * *", Type = nameof(TestJob) });
            }, (jobTypes) => jobTypes.AddTransient(nameof(TestJob), (sp) => new TestJob(async () =>
                             {
                                 jobRanEvent.Set();
                             }))).Build().RunAsync();


            var signalled = jobRanEvent.WaitOne(60000);
            Assert.True(signalled);
        }

        [Fact]
        public async Task Only_One_Instance_Of_Scheduled_Job_Executed_Concurrently()
        {
            int hostCount = 3;
            var jobRanEvent = new ManualResetEvent(false);
            var hosts = new List<IHost>();
            var lockProvider = new SingletonLockProvider();
            bool failed = false;

            for (int i = 0; i < hostCount; i++)
            {
                var host = CreateHostBuilder(lockProvider,
                    (scheduler) => scheduler.Jobs.Add("TestJob",
                        new ScheduledJobConfig() { Schedule = "* * * * *", Type = nameof(TestJob) }),
                        (jobTypes) => jobTypes.AddTransient(nameof(TestJob), (sp) => new TestJob(async () =>
                        {
                            if (!jobRanEvent.Set())
                            {
                                failed = true;
                            }
                            await Task.Delay(2000);
                        }))).Build();

                hosts.Add(host);
            }

            var tasks = hosts.Select(a => a.StartAsync());
            await Task.WhenAll(tasks);

            var jobRan = jobRanEvent.WaitOne(60000);
            Assert.True(jobRan);

            //  await Task.Delay(5000); // give more time for more jobs to run.
            Assert.False(failed);
        }


        [Fact]
        public void Can_Run_Overdue_Job()
        {

            var jobRanEvent = new AutoResetEvent(false);

            var mockAnchorStore = new MockAnchorStore();

            // simulate a job that is overdue.
            mockAnchorStore.CurrentAnchor = DateTime.UtcNow.AddDays(-1);

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<SchedulerConfig>((scheduler) =>
                        scheduler.Jobs.Add("TestJob", new ScheduledJobConfig()
                        {
                            Schedule = "* * * * *", // every minute.
                            Type = nameof(TestJob)
                        }));

                    services.AddScheduledJobs((options) => options.AddLockProviderInstance(new SingletonLockProvider())
                             .RegisterJobTypes((jobTypes) => jobTypes.AddTransient(nameof(TestJob), (sp) => new TestJob(async () => jobRanEvent.Set()))))
                    .AddSingleton<IAnchorStoreFactory>(new MockAnchorStoreFactory((jobName) =>
                    {

                        return mockAnchorStore;
                    }));

                }).Build().RunAsync();


            var signalled = jobRanEvent.WaitOne(60000);
            jobRanEvent.Reset();
            Assert.True(signalled);

            // should run again in another minute.          
            signalled = jobRanEvent.WaitOne(63000);
            Assert.True(signalled);

        }


        public static IHostBuilder CreateHostBuilder(
            ILockProvider lockProvider,
            Action<SchedulerConfig> configureScheduler,
            Action<NamedServiceRegistrationsBuilder<IJob>> registerJobTypes) =>

            Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure(configureScheduler);

                    services.AddScheduledJobs((options) => options.AddLockProviderInstance(lockProvider)
                            .RegisterJobTypes(registerJobTypes));
                });

        public class TestJob : IJob
        {
            private readonly Func<Task> _onJobExecuted;

            public TestJob(Func<Task> onJobExecuted)
            {
                _onJobExecuted = onJobExecuted;
            }

            public async Task ExecuteAsync(ExecutionInfo runInfo, CancellationToken token)
            {
                await _onJobExecuted();
            }
        }
    }
}
