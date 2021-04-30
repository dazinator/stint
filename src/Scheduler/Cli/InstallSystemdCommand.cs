namespace Scheduler.Cli
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Reflection;

    public class InstallSystemdCommand : Command
    {
        public const string CommandName = "install";

        private readonly SystemdConfigInstaller _installer;
        // private const string DefaultServiceUnitFileName = "IBlocklistDownloader.Service.service";

        public InstallSystemdCommand(SystemdConfigInstaller installer) : base(CommandName)
        {
            _installer = installer;
            var location = Assembly.GetEntryAssembly().Location;
            string locationWithoutFileExtension;
            if (location.EndsWith(".dll"))
            {
                locationWithoutFileExtension = location.Remove(location.Length - 4);
            }
            else
            {
                locationWithoutFileExtension = location;
            }

            Console.WriteLine(locationWithoutFileExtension);

            var startCommand = $"{locationWithoutFileExtension} start";

            var execStart = new Option<string>(
                "--exec-start",
                () => startCommand,
                "The command to launch the service executable.");

            AddOption(execStart);

            var user = new Option<string>(
                "--user",
                () => Environment.UserName,
                "The command to launch the service executable.");

            AddOption(user);

            var envDotnetRoot = new Option<string>(
                "--env-dotnet-root",
                "If the user does not have dotnet.exe on path specify the path to the directory where dotnet.exe can be found.");
            AddOption(envDotnetRoot);

            var reloadSystemd = new Option<bool>(
                "--reload",
                () => false,
                "Whether to reload systemd atfer installing the service unit file");

            AddOption(reloadSystemd);

            var pwd = new Option<string>(
                "--pwd",
                () => Path.GetDirectoryName(location),
                "The working directory in which the service will read its content / config.");
            AddOption(pwd);

            // Note that the parameters of the handler method are matched according to the names of the options
            Handler = CommandHandler.Create<string, string, string, bool, string>(
                async (execStart, user, envDotnetRoot, reload, pwd) =>
                {
                    try
                    {
                        var appName = Assembly.GetEntryAssembly()?.GetName().Name;
                        var serviceUnitFileName = $"{appName}.service";
                        await _installer.DeploySystemdConfig(execStart, user, envDotnetRoot, serviceUnitFileName, pwd,
                            appName);
                        if (reload)
                        {
                            Console.WriteLine("Reloading daemon..");
                            _installer.ReloadDaemon();
                            Console.WriteLine(
                                "Daemon reloaded successfully. Start service with sudo systemctl start {0}",
                                serviceUnitFileName);
                        }
                        else
                        {
                            Console.WriteLine(
                                "Not relaoding daemon.. You should reload before attempting to start the service. sudo systemctl daemon-reload");
                        }

                        return 0;
                    }
                    catch (UnauthorizedAccessException uex)
                    {
                        Console.WriteLine("{0} - Hint: try using sudo!", uex.Message);
                        return 1;
                    }
                });
        }
    }
}
