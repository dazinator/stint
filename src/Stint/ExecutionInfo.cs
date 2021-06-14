namespace Stint
{
    using System;

    public class ExecutionInfo
    {
        public ExecutionInfo(string name
           // DateTime? previousOccurrence,
           // DateTime occurrence,
           )
        {
            Name = name;
            // PreviousOccurrence = previousOccurrence;
            // Occurrence = occurrence;
        }

        /// <summary>
        /// The name for this job.
        /// </summary>
        public string Name { get; }
        // public DateTime? PreviousOccurrence { get; }
        // public DateTime Occurrence { get; }

    }
}
