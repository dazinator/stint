namespace Stint.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Cronos;
    using Dazinator.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Stint.Triggers.ManualInvoke;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Categories;

    [IntegrationTest]
    public class StintTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public StintTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            DefaultServices = new ServiceCollection();
            DefaultServices.AddLogging(a =>
            {
                a.AddXUnit(testOutputHelper);
                a.SetMinimumLevel(LogLevel.Debug);
            });
        }

        public ServiceCollection DefaultServices { get; set; }

        [Fact]
        public void Can_Run_Scheduled_Job()
        {
            var jobRanEvent = new AutoResetEvent(false);


            // services.Configure<SchedulerConfig>(configuration);
            //   a => a.AddTransient(nameof(TestJob), (sp) => new TestJob(onJobExecuted))

            var hostBuilderTask = CreateHostBuilder(new SingletonLockProvider(),
                    (config) => config.Jobs.Add("Can_Run_Scheduled_Job", new JobConfig()
                    {
                        Type = nameof(TestJob),
                        Triggers = new TriggersConfig()
                        {
                            Schedules =
                            {
                                new ScheduledTriggerConfig()
                                {
                                    Schedule = "* * * * *"
                                }
                            }
                        }
                    }),
                    (jobTypes) => jobTypes.AddTransient(nameof(TestJob), (sp) => new TestJob(async () => jobRanEvent.Set())))
                .Build()
                .RunAsync();


            var signalled = jobRanEvent.WaitOne(62000);
            Assert.True(signalled);
        }

        [Fact]
        public void Can_Set_Activity_GlobalTags()
        {
            var jobRanEvent = new AutoResetEvent(false);
            var capturedActivities = new ConcurrentBag<Activity>();

            // Set up ActivityListener to capture activities - must be done BEFORE creating the host
            using var activityListener = new ActivityListener
            {
                ShouldListenTo = source =>
                {
                    _testOutputHelper.WriteLine($"ActivitySource detected: {source.Name}");
                    return source.Name.StartsWith("Stint") || source.Name.Contains("job", StringComparison.OrdinalIgnoreCase);
                },
                Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                {
                    _testOutputHelper.WriteLine($"Activity sampling: {options.Name}");
                    return ActivitySamplingResult.AllDataAndRecorded;
                },
                ActivityStarted = activity =>
                {
                    _testOutputHelper.WriteLine($"Activity started: {activity.OperationName}, Tags: {string.Join(", ", activity.Tags.Select(t => $"{t.Key}={t.Value}"))}");
                    capturedActivities.Add(activity);
                }
            };

            ActivitySource.AddActivityListener(activityListener);

            var hostBuilderTask = CreateHostBuilder(new SingletonLockProvider(),
                configureJobsConfig:
                    (config) => config.Jobs.Add("Can_Set_Activity_GlobalTags", new JobConfig()
                    {
                        Type = nameof(TestJob),
                        Triggers = new TriggersConfig()
                        {
                            Schedules =
                            {
                        new ScheduledTriggerConfig()
                        {
                            Schedule = "* * * * *"
                        }
                            }
                        }
                    }),
                registerJobTypes:
                    (jobTypes) => jobTypes.AddTransient(nameof(TestJob), (sp) => new TestJob(async () => jobRanEvent.Set())),
                configureStint:
                    (builder) => {
                        // Replace this incorrect line:
                        // activityOptions.GlobalTags.Add(new ["TestTag", "TestValue"]);

                        // With the correct usage:
                        
                        builder.ConfigureActivityTags((activityOptions) => {
                            activityOptions.GlobalTags.Add(new KeyValuePair<string, object?>("TestTag", "TestValue"));
                            //activityOptions.GlobalTags.Add(new ["TestTag", "TestValue"]);
                        });

                    })
                .Build()
                .RunAsync();

            var signalled = jobRanEvent.WaitOne(62000);
            Assert.True(signalled);

            // Debug: Log all captured activities
            _testOutputHelper.WriteLine($"Captured {capturedActivities.Count} activities");
            foreach (var activity in capturedActivities)
            {
                _testOutputHelper.WriteLine($"Activity: {activity.OperationName}, Tags: {string.Join(", ", activity.Tags.Select(t => $"{t.Key}={t.Value}"))}");
            }

            // Based on the output, we can see that activities are created but don't have the global tags
            // Let's check specifically for the TestTag
            var activitiesWithGlobalTag = capturedActivities
                .Where(a => a.Tags.Any(tag => tag.Key == "TestTag" && tag.Value == "TestValue"))
                .ToList();

            // If this fails, it means the global tags configuration isn't working
            Assert.NotEmpty(activitiesWithGlobalTag);

            // Alternative assertion - check that at least the main job activity has the global tag
            var runJobOnceActivity = capturedActivities
                .FirstOrDefault(a => a.OperationName == "JobRunner.RunJobOnce");

            Assert.NotNull(runJobOnceActivity);
            Assert.Contains(runJobOnceActivity.Tags, tag => tag.Key == "TestTag" && tag.Value == "TestValue");
        }

        [Fact]
        public async Task Only_One_Instance_Of_Scheduled_Job_Executed_Concurrently()
        {
            var hostCount = 3;
            var jobRanEvent = new ManualResetEvent(false);
            var hosts = new List<IHost>();
            var lockProvider = new SingletonLockProvider();
            var failed = false;
            object isRunningDetection = null;


            for (var i = 0; i < hostCount; i++)
            {
                ILogger<StintTests> logger = null;

                var host = CreateHostBuilder(lockProvider,
                    (config) => config.Jobs.Add("Only_One_Instance_Of_Scheduled_Job_Executed_Concurrently", new JobConfig()
                    {
                        Type = nameof(TestJob),
                        Triggers = new TriggersConfig()
                        {
                            Schedules =
                            {
                                new ScheduledTriggerConfig()
                                {
                                    Schedule = "* * * * *"
                                }
                            }
                        }
                    }),
                    (jobTypes) => jobTypes.AddTransient(nameof(TestJob), (sp) => new TestJob(async () =>
                    {
                        logger?.LogInformation("TestJob Ran");
                        var thisInstance = new object();
                        var oldIsRunning = Interlocked.Exchange(ref isRunningDetection, thisInstance);
                        if (oldIsRunning != null)
                        {
                            // duplicate running
                            logger?.LogInformation("Another instance was already running..");
                            failed = true;
                        }

                        if (!jobRanEvent.Set())
                        {
                            logger?.LogInformation("Unable to set signal..");
                            failed = true;
                        }

                        logger?.LogInformation("Artificial job processing delay..");
                        await Task.Delay(2000);

                        oldIsRunning = Interlocked.Exchange(ref isRunningDetection, null);
                        if (oldIsRunning != thisInstance)
                        {
                            logger?.LogInformation("Another instance of the job ran before this one completed..");
                            // duplicate running
                            failed = true;
                        }
                    }))).Build();

                logger = host.Services.GetRequiredService<ILogger<StintTests>>();

                hosts.Add(host);
            }

            var tasks = hosts.Select(a => a.StartAsync());
            await Task.WhenAll(tasks);

            var jobRan = jobRanEvent.WaitOne(65000);
            Assert.True(jobRan);

            //   // give more time for more jobs to run.
            await Task.Delay(TimeSpan.FromSeconds(30));
            Assert.False(failed);
        }

        [Fact]
        public async Task Can_Run_Overdue_Job()
        {
            var jobRanEvent = new AutoResetEvent(false);

            var mockAnchorStore = new MockAnchorStore
            {
                // simulate a job that is overdue.
                CurrentAnchor = DateTime.UtcNow.AddDays(-1)
            };

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<JobsConfig>((config) => config.Jobs.Add("Can_Run_Overdue_Job", new JobConfig()
                    {
                        Type = nameof(TestJob),
                        Triggers = new TriggersConfig()
                        {
                            Schedules =
                            {
                                new ScheduledTriggerConfig()
                                {
                                    Schedule = "* * * * *"
                                }
                            }
                        }
                    }));

                    services.AddScheduledJobs((options) => options.AddLockProviderInstance(new SingletonLockProvider())
                            .RegisterJobTypes((jobTypes) => jobTypes.AddTransient(nameof(TestJob), (sp) => new TestJob(async () => jobRanEvent.Set()))))
                        .AddSingleton<IAnchorStoreFactory>(new MockAnchorStoreFactory((jobName) => mockAnchorStore));
                }).Build().RunAsync();


            var signalled = jobRanEvent.WaitOne(9000);
            jobRanEvent.Reset();
            Assert.True(signalled);

            // should run again in another minute.
            signalled = jobRanEvent.WaitOne(63000);

            Assert.True(signalled);
        }

        [Fact]
        public async Task Can_Chain_Jobs()
        {
            // var jobRanEvent = new AutoResetEvent(false)var chainedJobRanEvent = new AutoResetEvent(false);

            var jobRan = false;
            var jobTwoRan = false;

            var mockAnchors = new Dictionary<string, MockAnchorStore>()
            {
                {
                    "Can_Chain_Jobs", new MockAnchorStore
                    {
                        CurrentAnchor = DateTime.UtcNow.AddDays(-1)
                    }
                },
                {
                    "Can_Chain_Jobs_TestChainedJob", new MockAnchorStore
                    {
                        CurrentAnchor = DateTime.UtcNow.AddDays(-1)
                    }
                }
            };

            ILogger<StintTests> logger = null;

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    foreach (var service in DefaultServices)
                    {
                        services.Add(service);
                    }

                    services.Configure<JobsConfig>((config) =>
                    {
                        // overdue job will run immdiately
                        config.Jobs.Add("Can_Chain_Jobs", new JobConfig()
                        {
                            Type = nameof(TestJob),
                            Triggers = new TriggersConfig()
                            {
                                Manual = true,
                                //Schedules = {
                                //    new ScheduledTriggerConfig() {  Schedule = "* * * * *" }
                                //}
                            }
                        });

                        // we want this job to run off the back of the other job completing so we add a job completion trigger
                        config.Jobs.Add("Can_Chain_Jobs_TestChainedJob", new JobConfig()
                        {
                            Type = nameof(TestChainedJob),
                            Triggers = new TriggersConfig()
                            {
                                JobCompletions =
                                {
                                    new JobCompletedTriggerConfig()
                                    {
                                        JobName = "Can_Chain_Jobs"
                                    }
                                }
                            }
                        });
                    });

                    services.AddScheduledJobs(a => a.RegisterJobTypes((jobTypes) =>
                            jobTypes.AddTransient(nameof(TestJob), (sp) => new TestJob(() =>
                                {
                                    logger?.LogInformation("Can_Chain_Jobs Ran");
                                    jobRan = true;
                                    return Task.CompletedTask;
                                }))
                                .AddTransient(nameof(TestChainedJob), (sp) => new TestChainedJob(() =>
                                {
                                    logger?.LogInformation("Can_Chain_Jobs_TestChainedJob Ran");
                                    jobTwoRan = true;
                                    return Task.CompletedTask;
                                }))))
                        .AddSingleton<IAnchorStoreFactory>(new MockAnchorStoreFactory((jobName) => mockAnchors[jobName]));
                }).Build();

            logger = host.Services.GetRequiredService<ILogger<StintTests>>();

            var hostCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var hostRunTask = host.RunAsync(hostCts.Token);

            using (var scope = host.Services.CreateScope())
            {
                var manualTriggerInvoker = scope.ServiceProvider.GetRequiredService<IJobManualTriggerInvoker>();
                var success = false;

                // the issue here, is that if we trigger a job manually, but the JobRunner has not yet subscribed / picked up the next token
                // (there is a delay before it gets one on starting),
                // then our signal can be lost - so this won't reliably trigger the job.
                manualTriggerInvoker.Trigger("Can_Chain_Jobs");
                await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(10), hostCts.Token), Task.Run(async () =>
                {
                    while (!hostCts.IsCancellationRequested)
                    {
                        if (!jobRan)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1), hostCts.Token);
                            continue;
                        }

                        if (!jobTwoRan)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1), hostCts.Token);
                            continue;
                        }

                        success = true;
                    }

                    return false;
                }, hostCts.Token));

                Assert.True(success);
            }


            ////  signalled = chainedJobRanEvent.WaitOne(65000);
            //Assert.True(signalled);
        }

        [Theory]
        [InlineData("* * * * *", "23/01/2023 11:00", "23/01/2023 11:01")]
        [InlineData("*/10 7-9 * * *", "23/01/2023 07:10", "23/01/2023 07:20")] // 07:00 - 09:59 UTC – every 10 mins
        [InlineData("*/10 7-9 * * *", "23/01/2023 10:00", "24/01/2023 07:00")] // 07:00 - 09:59 UTC – every 10 mins - next occurrence tomorrow.
        [InlineData("*/30 10-13 * * *", "23/01/2023 10:10", "23/01/2023 10:30")] // 10:00 - 13:59 UTC – every 30 mins
        [InlineData("*/10 14 * * *", "23/01/2023 14:00", "23/01/2023 14:10")] // 14:00 - 14:59 UTC – every 10 mins
        public void Can_Use_Cron_Expression(string cron, string lastOccurrencUtc, string expectedNextOccurrenceUtc)
        {
            var expression = CronExpression.Parse(cron);

            var lastOccurrenceDateTime = DateTime.ParseExact(lastOccurrencUtc, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture).ToUniversalTime();

            var expectedNextOccurrenceDateTime = DateTime.ParseExact(expectedNextOccurrenceUtc, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);


            // var fromWhenShouldItNextRun = DateTime.UtcNow;
            var nextOccurence = expression.GetNextOccurrence(lastOccurrenceDateTime);

            Assert.Equal(expectedNextOccurrenceDateTime, nextOccurence);
        }

        [Exploratory]
        [Theory]
        [InlineData("*/1 * * * *", 180, 3)] // every minute
        public async Task Runs_To_Schedule_LongRunning(string cron, int testDurationInSeconds, int expectedRunCount)
        {
            var jobRanCount = 0;
            var successEvent = new AutoResetEvent(false);

            var hostBuilderTask = CreateHostBuilder(new SingletonLockProvider(),
                    (config) => config.Jobs.Add("Runs_To_Schedule_LongRunning", new JobConfig()
                    {
                        Type = nameof(TestJob),
                        Triggers = new TriggersConfig()
                        {
                            Schedules =
                            {
                                new ScheduledTriggerConfig()
                                {
                                    Schedule = cron
                                }
                            }
                        }
                    }),
                    (jobTypes) => jobTypes.AddTransient(nameof(TestJob), (sp) => new TestJob(async () =>
                    {
                        var totalRuns = Interlocked.Increment(ref jobRanCount);
                        if (totalRuns == expectedRunCount)
                        {
                            successEvent.Set();
                        }
                    })))
                .Build()
                .RunAsync();

            var waitTimeSpan = TimeSpan.FromSeconds(testDurationInSeconds + 10); // plus a buffer
            var signalled = successEvent.WaitOne(waitTimeSpan);
            Assert.True(signalled);
        }

        public IHostBuilder CreateHostBuilder(
            ILockProvider lockProvider,
            Action<JobsConfig> configureJobsConfig,
            Action<NamedServiceRegistrationsBuilder<IJob>> registerJobTypes,
            Action<StintServicesBuilder> configureStint = null
        ) =>
            Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    foreach (var service in DefaultServices)
                    {
                        services.Add(service);
                    }

                    services.Configure(configureJobsConfig);

                    services.AddScheduledJobs((options) =>
                    {
                        options.AddLockProviderInstance(lockProvider)
                                .RegisterJobTypes(registerJobTypes);

                        configureStint?.Invoke(options);

                    });
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
