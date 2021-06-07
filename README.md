# Stint

> a fixed period of time during which a person holds a job or position

Stint allows your existing dotnet application to run jobs.

## Features

- Scheduled jobs
  - Responsive to job config / schedule changing at runtime - jobs will be gracefully re-scheduled.
  - Jobs are run with cancellation token so can be cancelled gracefully.
  - Supports locking, so you can implement your own `ILockProvider` to prevent multiple instances of jobs running concurrently when scaling to multiple nodes for example.
    
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

Register your different job classes, with their own job type name which will be used to refer to them from configuration:

  ```csharp
   services.AddScheduledJobs(jobsConfigSection,
                        (r) => r.Include<MyCoolJob>(nameof(MyCoolJob), sp => new MyCoolJob()));
  ```

  

Note: You can use DI as usual for injecting dependencies into job classes.

Next we will configure the scheduler to run these jobs.

Edit the `appsettings.json` file:

```json
"JobsService": {
    "Jobs": {
      "TestJob":  {       
        "Schedule": "[CRON]",
        "Type" : "MyCoolJob"
      },
      "AnotherTestJob": {
       "Schedule": "[CRON]",
        "Type" : "MyCoolJob"
      },
      "DifferentJob": {
       "Schedule": "[CRON]",
        "Type" : "MyOtherCoolJob"
      }
    }   
  }

```

- Jobs have unique names - i.e "AnotherTestJob", "DifferentJob" etc as shown above.
- Each job has a "Type" which is a name that maps to a specific registered job class in the code - i.e "MyCoolJob", "MyOtherCoolJob" as shown above.
  This tells the job runner which job class to execute for this job.
- Each job has a "Schedule" - this is a CRON expression that determines when this scheduled job should run.
- You can change the configuration whilst the application is running and the changes will be applied in memory.

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

## How does scheduling work

After a scheduled job has been executed, a file / anchor is saved using the `IAnchorStore` implementation.
By default this saves an anchor file to your applications content root directory.
The anchor contains the date and time that the job last executed.
The scheduler compares the `schedule` you've specified, to the anchor file for the job, and works out when the job needs to be executed next. 
It then asynchronously delays until the appointed time.
If the job is scheduled to run every sunday, and you don't turn your machine on for a given day, when you turn it on next, and the job runner starts, 
the job is loaded into memory with the configured schedule. The scheduler sees that it's overdue based on the last anchor point. It will the execute the job immediately, and save a new anchor file. 
This ensures that overdue jobs are run in the case the application went down for a time etc.

### What about retries

The scheduler does not handle retries. If you need to retry, you should add that logic within the job. 
Once the job has completed - even if it throws an exception, the scheduler will drop a new anchor and not try to execute it again until the next appointed time.

### What about scaling?

If you run multiple instances of the job runner application, you'll want to configure the `ILockProvider` so that the same scheduled job doesn't run simulataneously on multiple nodes / processes.

Implement this interdace to use whatever distributed lock mechanism you want:

```
    public interface ILockProvider
    {
        Task<IDisposable> TryAcquireAsync(string name);
    }

```

For example, this could return an `IDisposable` representing a lock file, or a lock held by the database etc. The `name` argument is the job name.
You should return `null` if the lock cannot be acquired, in which case the scheduler will write a log entry, and skip running the job as it assumed it is already running somewhere else.

Then register your lock provider:

```csharp

services.AddScheduledJobs(jobsConfigSection,
                        (r) => r.Include<MyCoolJob>(nameof(MyCoolJob), sp => new MyCoolJob()))
        .AddLockProvider<MyFileLockProvider>();

```

The lock provider that is registered by default, is an empty lock provider, which means there is no locking, and jobs will be allowed to execute concurrently.
