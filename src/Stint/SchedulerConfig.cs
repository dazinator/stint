namespace Stint
{
    using System.Collections.Generic;

    public class SchedulerConfig
    {
        public SchedulerConfig() => Jobs = new Dictionary<string, ScheduledJobConfig>();

        public Dictionary<string, ScheduledJobConfig> Jobs { get; set; }
    }
}
