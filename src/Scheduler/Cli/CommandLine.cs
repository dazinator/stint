namespace Scheduler.Cli
{
    using System;
    using System.CommandLine;
    using System.Threading.Tasks;

    public class CommandLine : RootCommand
    {
        public CommandLine(Func<Task<int>> startAsyncCallback) : base("Scheduler cli")
        {
            AddCommand(new StartCommand(startAsyncCallback));
            // TODO: Only register systemd command when on linux, if on windows register an alternative command for installing using sc?
            AddCommand(new InstallSystemdCommand(new SystemdConfigInstaller()));
        }
    }
}
