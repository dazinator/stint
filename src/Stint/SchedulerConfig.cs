namespace Stint
{
    using System.Collections.Generic;

    public class SchedulerConfig
    {
        public Dictionary<string, JobConfig> Jobs { get; set; }
        // public List<JobConfig> Jobs { get; set; } = new List<JobConfig>();
    }
}
