namespace Stint
{
    using System;

    public class ExecutionInfo
    {
        public ExecutionInfo(string name,
            // DateTime? previousOccurrence,
            // DateTime occurrence,
            IJobOptionsStore optionsStore)
        {
            Name = name;
            // PreviousOccurrence = previousOccurrence;
            // Occurrence = occurrence;
            OptionsStore = optionsStore;
        }

        public string Name { get; }
        // public DateTime? PreviousOccurrence { get; }
        // public DateTime Occurrence { get; }
        private IJobOptionsStore OptionsStore { get; }

        public TOptions GetOptions<TOptions>()
            where TOptions : new() =>
            OptionsStore.GetOptions<TOptions>(Name);
    }
}
