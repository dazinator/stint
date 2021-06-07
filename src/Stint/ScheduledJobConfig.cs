namespace Stint
{
    using System;

    public class ScheduledJobConfig
    {
        public ScheduledJobConfig()
        {

        }

        public string Schedule { get; set; }

        public string Type { get; set; }

        public override bool Equals(object obj) => Equals(obj as ScheduledJobConfig);

        public bool Equals(ScheduledJobConfig obj)
        {
            var equal = obj != null && obj.Schedule == Schedule && obj.Type == Type;
            return equal;
        }

        public override int GetHashCode() => HashCode.Combine(Schedule, Type);
    }
}
