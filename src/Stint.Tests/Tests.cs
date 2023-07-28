namespace Stint.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AutoFixture;
    using Cronos;
    using Dazinator.Extensions.DependencyInjection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;
    using Stint.Docker;
    using Stint.Triggers.ManualInvoke;
    using Xunit;
    using Xunit.Categories;

    public partial class StintTests
    {
        [Fact]
        public void Can_Run_Scheduled_Job()
        {

            var jobRanEvent = new AutoResetEvent(false);


            // services.Configure<SchedulerConfig>(configuration);
            //   a => a.AddTransient(nameof(TestJob), (sp) => new TestJob(onJobExecuted))

            var hostBuilderTask = CreateHostBuilder(new SingletonLockProvider(),

            (config) => config.Jobs.Add("TestJob", new JobConfig()
            {
                Type = nameof(TestJob),
                Triggers = new TriggersConfig()
                {
                    Schedules = {
                         new ScheduledTriggerConfig() {  Schedule = "* * * * *" }
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
        public async Task Only_One_Instance_Of_Scheduled_Job_Executed_Concurrently()
        {
            var hostCount = 3;
            var jobRanEvent = new ManualResetEvent(false);
            var hosts = new List<IHost>();
            var lockProvider = new SingletonLockProvider();
            var failed = false;

            for (var i = 0; i < hostCount; i++)
            {
                var host = CreateHostBuilder(lockProvider,

                (config) => config.Jobs.Add("TestJob", new JobConfig()
                {
                    Type = nameof(TestJob),
                    Triggers = new TriggersConfig()
                    {
                        Schedules = {
                            new ScheduledTriggerConfig() {  Schedule = "* * * * *" }
                        }
                    }
                }),
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

            var jobRan = jobRanEvent.WaitOne(65000);
            Assert.True(jobRan);

            //  await Task.Delay(5000); // give more time for more jobs to run.
            Assert.False(failed);
        }

        [Fact]
        public void Can_Run_Overdue_Job()
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

                    services.Configure<JobsConfig>((config) => config.Jobs.Add("TestJob", new JobConfig()
                    {
                        Type = nameof(TestJob),
                        Triggers = new TriggersConfig()
                        {
                            Schedules = {
                                new ScheduledTriggerConfig() {  Schedule = "* * * * *" }
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
        public void Can_Chain_Jobs()
        {

            var jobRanEvent = new AutoResetEvent(false);
            var chainedJobRanEvent = new AutoResetEvent(false);

            var mockAnchors = new Dictionary<string, MockAnchorStore>()
            {
                {"TestJob", new MockAnchorStore  { CurrentAnchor = DateTime.UtcNow.AddDays(-1) } },
                {"TestChainedJob", new MockAnchorStore  { CurrentAnchor = DateTime.UtcNow.AddDays(-1) } }
            };

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((hostContext, services) =>
                {

                    services.Configure<JobsConfig>((config) =>
                    {

                        // overdue job will run immdiately
                        config.Jobs.Add("TestJob", new JobConfig()
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
                        config.Jobs.Add("TestChainedJob", new JobConfig()
                        {
                            Type = nameof(TestChainedJob),
                            Triggers = new TriggersConfig()
                            {
                                JobCompletions = {
                                    new JobCompletedTriggerConfig(){ JobName ="TestJob" }
                                }
                            }
                        });
                    });

                    services.AddScheduledJobs(a => a.RegisterJobTypes((jobTypes) =>
                                  jobTypes.AddTransient(nameof(TestJob), (sp) => new TestJob(async () => jobRanEvent.Set()))
                                          .AddTransient(nameof(TestChainedJob), (sp) => new TestChainedJob(async () => chainedJobRanEvent.Set()))

                                ))
                                .AddSingleton<IAnchorStoreFactory>(new MockAnchorStoreFactory((jobName) => mockAnchors[jobName]));
                }).Build();

            var hostTask = host.RunAsync();
            var manualTriggerInvoker = host.Services.GetRequiredService<IJobManualTriggerInvoker>();
            manualTriggerInvoker.Trigger("TestJob");

            var signalled = jobRanEvent.WaitOne(9000);
            Assert.True(signalled);

            signalled = chainedJobRanEvent.WaitOne(65000);
            Assert.True(signalled);

        }

        [Theory]
        [InlineData("* * * * *", "23/01/2023 11:00", "23/01/2023 11:01")]
        [InlineData("*/10 7-9 * * *", "23/01/2023 07:10", "23/01/2023 07:20")] // 07:00 - 09:59 UTC – every 10 mins
        [InlineData("*/10 7-9 * * *", "23/01/2023 10:00", "24/01/2023 07:00")] // 07:00 - 09:59 UTC – every 10 mins - next occurrence tomorrow.
        [InlineData("*/30 10-13 * * *", "23/01/2023 10:10", "23/01/2023 10:30")]  // 10:00 - 13:59 UTC – every 30 mins
        [InlineData("*/10 14 * * *", "23/01/2023 14:00", "23/01/2023 14:10")]   // 14:00 - 14:59 UTC – every 10 mins
        public void Can_Use_Cron_Expression(string cron, string lastOccurrencUtc, string expectedNextOccurrenceUtc)
        {
            var expression = CronExpression.Parse(cron);

            var lastOccurrenceDateTime = DateTime.ParseExact(lastOccurrencUtc, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture).ToUniversalTime();

            var expectedNextOccurrenceDateTime = DateTime.ParseExact(expectedNextOccurrenceUtc, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);


            // var fromWhenShouldItNextRun = DateTime.UtcNow; 
            var nextOccurence = expression.GetNextOccurrence(lastOccurrenceDateTime);

            Assert.Equal(expectedNextOccurrenceDateTime, nextOccurence);

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

        [Exploratory]
        public async Task Serialise_Docker_ServiceCreateParameters()
        {
            // global:Docker.DotNet.DockerClient client = new global:Docker.DotNet.DockerClientConfiguration(new Uri("tcp://"))

            var fixture = new Fixture();
            var someDto = fixture.Create<global::Docker.DotNet.Models.ServiceCreateParameters>();
            var json = System.Text.Json.JsonSerializer.Serialize(someDto);


            //var swarmServiceCreate = new global::Docker.DotNet.Models.ServiceCreateParameters()
            //{
            //    Service = new global::Docker.DotNet.Models.ServiceSpec()
            //    {
            //        EndpointSpec = new global::Docker.DotNet.Models.EndpointSpec()
            //        {
            //            Ports = new List<global::Docker.DotNet.Models.PortConfig>()
            //            {
            //                new global::Docker.DotNet.Models.PortConfig()
            //                {
            //                    Protocol = "Tcp",
            //                    PublishedPort = 80,
            //                    TargetPort = 80
            //                }
            //            }
            //        },
            //        Labels = new Dictionary<string, string>()
            //        {
            //            { "com.docker.stack.namespace", "test" }
            //        },
            //        Mode = new global::Docker.DotNet.Models.ServiceMode()
            //        {
            //            ReplicatedJob = new global::Docker.DotNet.Models.ReplicatedJob
            //            {
            //                MaxConcurrent = 1,
            //                TotalCompletions = 1,    
            //            },                        
            //        },
            //        Name = "test",
            //        Networks = new List<global::Docker.DotNet.Models.NetworkAttachmentConfig>()
            //        {
            //            new global::Docker.DotNet.Models.NetworkAttachmentConfig()
            //            {
            //                Target = "test",
            //                Aliases = new List<string>()
            //                {
            //                    "test"
            //                },
            //                DriverOpts = new Dictionary<string, string>()
            //                {
            //                    { "com.docker.network.driver.overlay.vxlanid_list", "4096" }
            //                }        
            //            }
            //        },
            //        TaskTemplate = new global::Docker.DotNet.Models.TaskSpec()
            //        {
            //            ContainerSpec = new global::Docker.DotNet.Models.ContainerSpec()
            //            {
            //                Image = "nginx:latest",
            //                Labels = new Dictionary<string, string>()
            //                {
            //                    { "com.docker.stack.namespace", "test" }
            //                },
            //                Env = new List<string>()
            //                {
            //                    "VIRTUAL_HOST=test",
            //                    "VIRTUAL_PORT=80"
            //                },
            //                Hosts = new List<string>()
            //                {
            //                    "test"
            //                },
            //                Args = new List<string>()
            //                {
            //                    "test"
            //                },
            //                CapabilityAdd = new List<string>()
            //                {
            //                    "test"
            //                },
            //                CapabilityDrop = new List<string>()
            //                {
            //                    "test"
            //                },
            //                Command = new List<string>()
            //                {
            //                    "test"
            //                },
            //                DNSConfig = new global::Docker.DotNet.Models.DNSConfig()
            //                {
            //                    Nameservers = new List<string>()
            //                    {
            //                        "test"
            //                    },
            //                    Options = new List<string>()
            //                    {
            //                        "test"
            //                    },
            //                    Search = new List<string>()
            //                    {
            //                        "test"
            //                    }
            //                },


            //            }
            //        }


            //    }
            //};


            //var swarmClient = new global::Docker.DotNet.DockerClientConfiguration(new Uri("tcp://"))
            // {
            //     Service = new Docker.DotNet.Models.ServiceSpec()
            //     {


            //     }
            // }
        }

        [Exploratory]
        [Fact]
        public async Task Can_Configure_DockerSwarmJob()
        {
            var builder = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config.AddJsonFile("swarmjobtest.json");
                    //config.AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var config = hostContext.Configuration;
                    services.Configure<JobsConfig>(config.GetSection("Scheduler"));

                    services.AddScheduledJobs((options) => options.AddLockProviderInstance(new SingletonLockProvider())
                            .RegisterJobTypes((jobTypes) =>
                            {
                                jobTypes.AddTransient<SwarmJob>(nameof(SwarmJob));
                            }));

                    // configure the swarm services for the swarm jobs
                    services.ConfigureUponRequest<global::Docker.DotNet.Models.ServiceCreateParameters>().From((name) =>
                    {
                        // configure your options for the requested name without having to register this name in advance.
                        var section = $"Scheduler:Jobs:{name}:Config:ServiceCreateParameters";
                        return config.GetSection(section);
                    });

                    // docker client
                    services.AddSingleton<global::Docker.DotNet.DockerClient>(sp =>
                    {
                        var hostAddress = config.GetConnectionString("DockerHost");
                        global::Docker.DotNet.DockerClientConfiguration dockerConfig = null;
                        if (string.IsNullOrEmpty(hostAddress))
                        {
                            dockerConfig = new global::Docker.DotNet.DockerClientConfiguration();
                        }
                        else
                        {
                            dockerConfig = new global::Docker.DotNet.DockerClientConfiguration(new Uri(hostAddress), null, default);
                        }
                        var client = dockerConfig.CreateClient();
                        return client;
                    });
                });

            await builder.Build().RunAsync();

        }
    }
}
