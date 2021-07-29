# Stint

> a fixed period of time during which a person holds a job or position

Stint allows your existing dotnet application to run jobs.

## Features

- Jobs:
  - Are configured using the `IOptions` pattern so can be configured from a wide variety of sources, and are responsive to config changes at runtime.
  - Are classes with async methods which are run with cancellation tokens so you can exit gracefully, if for example the host is shutting down, or the job needs to be terminated for a config reload.
  - Support locking, so you can implement your own `ILockProvider` to prevent multiple instances of a job from being signalled concurrently when scaling to multiple nodes. Default `ILockProvider` provides a no-op lock.
    
- triggers
  - Schedule (cron)
  - Manual invocation (i.e inject `IJobManualTriggerInvoker` and call `bool Trigger(string jobName)` )
  - JobCompletion (i.e a job can be automatically triggered when another job with the specified name completes)

# Getting Started

Implement a job class. This is just a class that implements the `IJob` interface:

  ```csharp

        public class MyCoolJob : IJob
        {
           
            private ILogger _logger;

            public TestJob(ILogger logger)
            {
              _logger = logger;
            }

            public Task ExecuteAsync(ExecutionInfo runInfo, CancellationToken token)
            {
               _logger.LogDebug("Working..");
                return Task.CompletedTask;
            }
        }


  ```
    
Note: You can use DI as usual for injecting dependencies into job classes.

 Add `AddScheduledJobs` services, and register your available job classes.
 
 ```csharp

     services.AddScheduledJobs((options) => options.RegisterJobTypes((jobTypes) => 
            jobTypes.AddTransient(nameof(MyCoolJob), (sp) => new MyCoolJob())
                    .AddTransient<MyOtherCoolJob>(nameof(MyOtherCoolJob))))
  
  ```

  Each Job class is registered with a job type name, which is used to refer to it when configuring jobs of that type.

  Next configure your job instances, and their triggers.
  This uses the standard `IOptions` pattern, so you can bind the config from `Json` config, pre or post configure hooks, or any other sources that support this pattern.
    
  ```csharp
  services.Configure<JobsConfig>((config) =>
                    {                     
                        config.Jobs.Add("TestJob", new JobConfig()
                        {
                            Type = nameof(TestJob),
                            Triggers = new TriggersConfig()
                            {
                                Schedules = {
                                    new ScheduledTriggerConfig() {  Schedule = "* * * * *" }
                                }
                            }
                        });

                        // example of chaining, this job has a trigger that causes it to run when the other job completes.
                        config.Jobs.Add("TestChainedJob", new JobConfig()
                        {
                            Type = nameof(TestJob),
                            Triggers = new TriggersConfig()
                            {
                                JobCompletions = {
                                    new JobCompletedTriggerConfig(){ JobName ="TestJob" }
                                }
                            }
                        });
                    });

  ``` 

  You can add multiple triggers for each job. The job will run when any of the triggers signal. 
  So if you add a schedule trigger, and a JobCompletion trigger, the job will run when either the schedule trigger signals its time, or the specified job completes for the completion trigger.

 ### Manual triggers

 To allow manually triggering a job, you have to enable the `Manual` trigger:

 ```csharp
  config.Jobs.Add("TestChainedJob", new JobConfig()
                        {
                            Type = nameof(TestJob),
                            Triggers = new TriggersConfig()
                            {
                                Manual = true,
                                JobCompletions = {
                                    new JobCompletedTriggerConfig(){ JobName ="TestJob" }
                                }
                            }
                        });
```

You can then trigger the job to run from a button click or api call or any other event in your application:

```csharp

 IJobManualTriggerInvoker manualTriggerInvoker = GetOrInjectThisService<IJobManualTriggerInvoker>();
 bool triggered = manualTriggerInvoker.Trigger("TestChainedJob");

```

Note: `triggered` will be false if the job name specified does not have a manual trigger enabled.

## Using config

If you want to bind the scheduler jobs to a json config file, you'll json will need to look like this:

```json

"Stint": {
    "Jobs": {
      "TestJob": {
        "Type": "MyCoolJob",
        "Triggers": {
          "Schedules": [
            { "Schedule": "* * * * *" }
          ],
          "JobCompletions": [
            { "JobName": "AnotherTestJob" }
          ]
        }
      },
      "AnotherTestJob": {
        "Type": "MyCoolJob",
        "Triggers": {
          "Schedules": [
            { "Schedule": "* * * * *" }
          ],
          "Manual":  true
        }
      }
    }
  }

```

- Jobs have unique names - i.e "AnotherTestJob", "DifferentJob" etc as shown above.
- Each job has a "Type" which is a name that maps to a specific registered job class in the code - i.e "MyCoolJob" as shown above.
  This tells the job runner which job class to execute for this job.
- Each job has a `Triggers` section where different kinds of triggers can be configured for the job.
- You can change the configuration whilst the application is running and the scheduler will reload / reconfigure any necessary jobs in memory as necessary to reflect latest configuration. If a jobs configuration is updated and it is currently executing, it will be signalled for cancellation.

## Schedule Syntax (cron)


For the CRON expression syntax, see: https://github.com/HangfireIO/Cronos#cron-format

```ascii
                                       Allowed values    Allowed special characters   Comment

┌───────────── second (optional)       0-59              * , - /                      
│ ┌───────────── minute                0-59              * , - /                      
│ │ ┌───────────── hour                0-23              * , - /                      
│ │ │ ┌───────────── day of month      1-31              * , - / L W ?                
│ │ │ │ ┌───────────── month           1-12 or JAN-DEC   * , - /                      
│ │ │ │ │ ┌───────────── day of week   0-6  or SUN-SAT   * , - / # L ?                Both 0 and 7 means SUN
│ │ │ │ │ │
* * * * * *
```

## How does job scheduling work

After a scheduled job has been executed, a file / anchor is saved using the `IAnchorStore` implementation, which by default saves an anchor file to your applications content root directory.
The anchor contains the date and time that the job last executed.
Jobs that have scheduled triggers, compare the configured `schedule` you've specified, to the anchor file for the job.
- If there is no anchor file then it is assumed the job has never been run, and the next occurrence will be calculated from `now`.
- If there is an anchor file, then the next occurrence is calculated from that last anchor time. 
If the next occurrence is calculated to be in the past (i.e becuase there was an anchor file, but the current configured schedule should dictate the job has run since then) then the job is presumed to be `overdue` and it will be run immdiately.
If the next occurrence is in the future, then the scheduler asynchronously delays until the next occurrence.

### What about retries

The scheduler does not handle retries. If you need to retry, you should add that logic within your job itself.
Once the job has completed - even if it throws an exception, the scheduler will drop a new anchor and not try to execute it again until the next appointed time.

### What about scaling?

#### Locking

If you run multiple instances of the job runner application, you'll want to configure the `ILockProvider` so that the same scheduled job doesn't run simulataneously on multiple nodes / processes.

Implement this interface to use whatever distributed lock mechanism you want:

```csharp
    public interface ILockProvider
    {
        Task<IDisposable> TryAcquireAsync(string name);
    }

```

For example, this could return an `IDisposable` representing a lock file, or a lock held by the database etc. The `name` argument is the job name.
You should return `null` if the lock cannot be acquired, in which case the scheduler will write a log entry, and skip running the job as it assumed it is already running somewhere else.

Then register your lock provider:

```csharp

 services.AddScheduledJobs((options) => options.RegisterJobTypes((jobTypes) => 
            jobTypes.AddTransient(nameof(MyCoolJob), (sp) => new MyCoolJob())
                    .AddTransient<MyOtherCoolJob>(nameof(MyOtherCoolJob)))
            options.AddLockProvider<MyFileLockProvider>());        


```

The lock provider that is registered by default, is an empty lock provider, which means there is no locking, and jobs will be allowed to execute simultaneosly.

#### Events

`Job Completion` triggers use a `pub sub` mechanism. 
When a job has completed, an event is published with the name of the job that completed. 
The `Job Completion` trigger subscribes to this event, and triggers when the completed job name matches the job name for the trigger.

All this means, job chaning works by default in the same process, becuase the pub / sub mechanism is in process, and is not distrubuted.
If you want to allow other worker nodes to run jobs in the chain you'll have to register custom implementations of `IPublisher<JobCompletedEventArgs>' and `ISubscriber<JobCompletedEventArgs>'.
When the job completed message is published, you can then take control of the publish and publish a message to a distributed pub sub system. 
Likewise when the JobCompletion trigger subscribes you can take control of the subscription and subsribe to your distributed pub sub topic.
