namespace Scheduler
{
    using System;

    public class JobConfig
    {
        public JobConfig(string type, string schedule)
        {
            Type = type;
            Schedule = schedule;
        }

        public string Schedule { get; set; }

        // public List<string> InputLists { get; set; }
        //  public string Name { get; set; }

        public string Type { get; }

        public override bool Equals(object obj) => Equals(obj as JobConfig);

        public bool Equals(JobConfig obj)
        {
            var equal = obj != null && obj.Schedule == Schedule && obj.Type == Type;
            return equal;
        }

        public override int GetHashCode() => HashCode.Combine(Schedule, Type);
    }
}
