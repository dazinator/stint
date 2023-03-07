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
    using Microsoft.Extensions.Options;
    using Xunit;

    public partial class JobChangeTokenProducerFactoryTests
    {
        [Fact(Skip = "Not yet working consistently on github build server")]
        public void Can_Get_ChangeToken_ForJobWithMultipleSchedules()
        {

            var jobRanEvent = new AutoResetEvent(false);

            var hoursNow = DateTime.UtcNow.Hour;

            // services.Configure<SchedulerConfig>(configuration);
            //   a => a.AddTransient(nameof(TestJob), (sp) => new TestJob(onJobExecuted))

            var host = CreateHostBuilder(new SingletonLockProvider(),
            //  JobsConfig jobsConfig
            (config) => config.Jobs.Add("TestJob", new JobConfig()
            {
                Type = nameof(TestJob),
                Triggers = new TriggersConfig()
                {
                    Schedules = {
                          new ScheduledTriggerConfig() {  Schedule = $"*/1 {hoursNow}-{hoursNow+1} * * *" },
                          new ScheduledTriggerConfig() {  Schedule = $"*/1 {hoursNow-2}-{hoursNow-1} * * *" } // in the past means next occurence is tomorrow
                       //  new ScheduledTriggerConfig() {  Schedule = "*/10 14 * * *" }
                    }
                }
            }),

                (jobTypes) => jobTypes.AddTransient(nameof(TestJob), (sp) => new TestJob(async () => jobRanEvent.Set())))
                .Build();


            var sut = host.Services.GetRequiredService<IJobChangeTokenProducerFactory>();
            var config = host.Services.GetRequiredService<IOptionsSnapshot<JobsConfig>>();
            var jobConfig = config.Value.Jobs["TestJob"];

            var changeTokenProducer = sut.GetChangeTokenProducer("TestJob", jobConfig, default);
            // changeTokenProducer.Produce()
            var token = changeTokenProducer.Produce();
            var listening = token.RegisterChangeCallback((s) => jobRanEvent.Set(), null);

            var signalled = jobRanEvent.WaitOne(62000);
            Assert.True(signalled);
        }


        public static IHostBuilder CreateHostBuilder(
        ILockProvider lockProvider,
        Action<JobsConfig> configureScheduler,
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

            public TestJob(Func<Task> onJobExecuted) => _onJobExecuted = onJobExecuted;

            public async Task ExecuteAsync(ExecutionInfo runInfo, CancellationToken token) => await _onJobExecuted();
        }

        public class TestChainedJob : IJob
        {
            private readonly Func<Task> _onJobExecuted;

            public TestChainedJob(Func<Task> onJobExecuted) => _onJobExecuted = onJobExecuted;

            public async Task ExecuteAsync(ExecutionInfo runInfo, CancellationToken token) => await _onJobExecuted();
        }
    }
}
