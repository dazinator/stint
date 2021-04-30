namespace Stint.Cli

{
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    public class StartCommand : Command
    {
        private const string CommandName = "start";

        public StartCommand(Func<Task<int>> startAysncCallback) : base(CommandName) =>
            Handler = CommandHandler.Create(async () =>
            {
                await startAysncCallback();
            });
    }
}
