# Jobs

After installing the service, you can make changes to its job configuration, even whilst its running (it will respond to any changes appropriately)

Edit the `appsettings.json` file:

Edit this section:

```json
"JobsService": {
    "Subscription": {
      "Username": "foo",
      "Pin": 12345
    },
    "Jobs": [
      {
        "InputLists": [
          "aowijdawiod",
          "adawdwa"
        ],
        "Output": "single.p2p",
        "Schedule": "[CRON]"
      },
      {
        "InputLists": [
          "aowijdawiod",
          "adawdwa"
        ],
        "Output": "single.p2p",
        "Schedule": "[CRON]"
      }
    ]
  }

```

Put in your IBlocklist subscription details.
Under the "Jobs" section you can have multiple jobs configured, each has:

- InputLists - these are the IBlocklist lists that you want the job to download.
- Output - this is the filename you want all of the lists to be combined into and output.
- Schedule - this is a CRON expression that determines when you want this to happen.

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

When a job is successfully executed, a file / anchor is saved alongside the output list. This anchor file contains the date and time that the job last executed.

If the job is scheduled to run every sunday, and you don't turn your machine on that sunday, when you run your machine on the following monday, and this service starts, the job is loaded. The job checks for the anchor file and can see that it last ran the sunday before. It sees its meant to run every sunday, but the time its meant to run is in the past (yesterday). In this scenario, it will run the job immediately to "catch" up, and then it will save a new anchor file. This ensures that overdue jobs are run and your lists are kept up to date.
