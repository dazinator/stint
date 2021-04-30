# Stint

> a fixed period of time during which a person holds a job or position

Stint allows your existing dotnet application to run jobs.

  - `Stint.Cli` allows you to extend your existing `Main` application entrypoint, to support some additional commands used for installing your application as a 
    `systemd` service on linux, or a windows service on windows. This makes it super easy for someone to download your application to their linux or windows machine and then run it with some command line arguments to have it installed as a service on their machine - so it's ready to run jobs appropriately.
  - `Stint.Scheduler` provides all the classes needed to create and configure a scheduler, which will execute jobs.
 
# Getting Started
- TODO
