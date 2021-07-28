namespace Stint
{
    using System.Collections.Generic;

    public class TriggersConfig
    {
        public TriggersConfig()
        {
            Schedules = new List<ScheduledTriggerConfig>();
            JobCompletions = new List<JobCompletedTriggerConfig>();
        }
        public List<ScheduledTriggerConfig> Schedules { get; set; }
        public List<JobCompletedTriggerConfig> JobCompletions { get; set; }

        /// <summary>
        /// Whether the job can be manually triggered.
        /// </summary>
        public bool Manual { get; set; } = false;
        //private int GetTriggersHashCode()
        //{
        //    unchecked
        //    {
        //        var hash = 19;
        //        foreach (var foo in ScheduledTriggers)
        //        {
        //            hash = (hash * 31) + foo.GetHashCode();
        //        }
        //        return hash;
        //    }
        //}

        public bool Equals(TriggersConfig obj)
        {
            var equal = obj != null &&
                obj.Schedules.ScrambledEquals(this.Schedules)
                && obj.JobCompletions.ScrambledEquals(this.JobCompletions);
            return equal;
        }
    }

}
