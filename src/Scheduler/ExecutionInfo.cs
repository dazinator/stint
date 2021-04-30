namespace Scheduler
{
    using System;

    public class ExecutionInfo
    {
        public ExecutionInfo(string name, DateTime? previousOccurrence, DateTime occurrence,
            IJobSettingsStore optionsStore)
        {
            Name = name;
            PreviousOccurrence = previousOccurrence;
            Occurrence = occurrence;
            OptionsStore = optionsStore;
        }

        public string Name { get; }
        public DateTime? PreviousOccurrence { get; }
        public DateTime Occurrence { get; }
        private IJobSettingsStore OptionsStore { get; }

        public TOptions GetOptions<TOptions>()
            where TOptions : new() =>
            OptionsStore.GetOptions<TOptions>(Name);
    }
}
