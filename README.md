# Stint

> a fixed period of time during which a person holds a job or position

Stint allows your existing dotnet application to run jobs.

  - `Stint.Cli` if you want your application to run as a service on linux or windows, then this package is for you. It allows you to extend your existing program `Main` entrypoint, to support some additional commands that can be used to install your application as a 
    `systemd` service on linux, or a windows service on windows. This makes it super easy for someone to download your application to their linux or windows machine and then run it with some command line arguments to have it installed as a service on their machine. If your application is hosted in a different way, such as in IIS, then you won't need this.
  - `Stint.Scheduler` provides the functionality needed so you can include a job runner in your application.
 
## Features

- Scheduled jobs
  - Responsive to job config / schedule changing at runtime - jobs will be instantly re-scheduled.
  - Jobs are run with cancellation token so can be cancelled gracefully.
    
# Getting Started
- TODO
