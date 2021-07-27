namespace Stint
{
    using System.Collections.Generic;

    public class JobsConfig
    {
        public JobsConfig() => Jobs = new Dictionary<string, JobConfig>();

        public Dictionary<string, JobConfig> Jobs { get; set; }
    }
}
