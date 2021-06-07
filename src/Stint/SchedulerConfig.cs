namespace Stint
{
    using System.Collections.Generic;

    public class SchedulerConfig
    {
        public Dictionary<string, ScheduledJobConfig> Jobs { get; set; }
    }
}
