# Jobs

Scheduled jobs are configured. You can change the configuration whilst the application is running and the changes will be applied in memory.

Edit the `appsettings.json` file:

Edit this section:

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

Under the "Jobs" section, each property is the unique name for the scheduled job.
Each named job then has:

- Schedule: this is a CRON expression that determines when this scheduled job should run.
- Type: this identifies the type of the job to run. Job's are implemented as classes, so you might have different types of jobs to do different types of things. The job type specified here must align with the job type name registered on startup:

  ```csharp
   services.AddScheduledJobs(jobsConfigSection,
                        (r) => r.Include<MyCoolJob>(nameof(MyCoolJob), sp => new MyCoolJob()));
  ```

  A job class is just a class that implements the `IJob` interface:

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

After a scheduled job has been executed, a file / anchor is saved.
The anchor contains the date and time that the job last executed.
If the job is scheduled to run every sunday, and you don't turn your machine on that sunday,
when you turn it on monday and the job runner starts, the job is loaded into memory with the configured schedule.as it's overdue. It will save a new anchor file. 
This ensures that overdue jobs are run in the case the application went down for a time etc.

### What about retries

The scheduler does not handle retries. If you need to retry, you should add that logic within the job. 
Once the job has completed - even if it throws an exception, the scheduler will drop a new anchor and not try to execute it again until the next appointed time.
