namespace Stint
{
    using System.Collections.Generic;

    public class JobsConfig
    {
        public JobsConfig() => Jobs = new Dictionary<string, ScheduledJobConfig>();

        public Dictionary<string, ScheduledJobConfig> Jobs { get; set; }
    }
}
