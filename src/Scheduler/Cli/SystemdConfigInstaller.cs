namespace Scheduler.Cli
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Extensions.FileProviders;

    public class SystemdConfigInstaller
    {
        private const string TargetDir = "/etc/systemd/system/";

        public async Task DeploySystemdConfig(string execStart, string user, string envDotnetRoot,
            string serviceUnitFileName, string workingDirectory, string syslogIdentifier)
        {
            var manifestEmbeddedProvider = new ManifestEmbeddedFileProvider(typeof(SystemdConfigInstaller).Assembly);
            var template = manifestEmbeddedProvider.GetFileInfo("/embedded/systemd.service");
            var templateText = string.Empty;

            using (var reader = new StreamReader(template.CreateReadStream()))
            {
                templateText = await reader.ReadToEndAsync();
            }

            // replace tokens
            templateText = templateText.Replace("{start-command}", execStart);
            templateText = templateText.Replace("{username}", user);
            templateText = templateText.Replace("{env-dotnet-root}", envDotnetRoot ?? string.Empty);
            templateText = templateText.Replace("{working-dir}", workingDirectory ?? "/");
            templateText = templateText.Replace("{SyslogIdentifier}", syslogIdentifier);

            // deploy
            var path = Path.Combine(TargetDir, serviceUnitFileName);
            await File.WriteAllTextAsync(path, templateText);
            Console.WriteLine("Service unit file written to: {0}", path);
            Console.WriteLine(templateText);
        }

        public void ReloadDaemon()
        {
            // systemctl daemon-reload
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = "systemctl";
            startInfo.Arguments = "daemon-reload";
            startInfo.UseShellExecute = false;
            //Set output of program to be written to process output stream
            startInfo.RedirectStandardOutput = true;
            //Optional
            startInfo.WorkingDirectory = Environment.CurrentDirectory;

            using (var process = Process.Start(startInfo))
            {
                var strOutput = process.StandardOutput.ReadToEnd();
                //Wait for process to finish
                process.WaitForExit();
                Console.Write(strOutput);
            }
            // sudo systemctl start HelloWorld
        }
    }
}
