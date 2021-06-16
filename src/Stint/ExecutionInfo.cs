namespace Stint
{
    using System;

    public class ExecutionInfo
    {
        public ExecutionInfo(string name) => Name = name;

        /// <summary>
        /// The name for this job.
        /// </summary>
        public string Name { get; }
    }
}
