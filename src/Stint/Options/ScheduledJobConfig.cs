namespace Stint
{
    using System;

    public class ScheduledJobConfig
    {
        public ScheduledJobConfig() => Triggers = new TriggersConfig();

        //public List<ScheduledTriggerConfig> ScheduledTriggers { get; set; }
        //public List<JobCompletedTriggerConfig> OnJobCompletedTriggers { get; set; }

        public TriggersConfig Triggers { get; set; }

        //public string Schedule { get; set; }

        public string Type { get; set; }

        public override bool Equals(object obj) => Equals(obj as ScheduledJobConfig);

        public bool Equals(ScheduledJobConfig obj)
        {
            var equal = obj != null &&
                obj.Type == Type &&
                obj.Triggers.Equals(this.Triggers);
            return equal;
        }

        //public override int GetHashCode() => HashCode.Combine(Type, GetTriggersHashCode());

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

    }

}
