namespace Stint
{
    using System;

    public class JobConfig
    {
        public JobConfig()
        {

        }

        public string Schedule { get; set; }

        public string Type { get; set; }

        public override bool Equals(object obj) => Equals(obj as JobConfig);

        public bool Equals(JobConfig obj)
        {
            var equal = obj != null && obj.Schedule == Schedule && obj.Type == Type;
            return equal;
        }

        public override int GetHashCode() => HashCode.Combine(Schedule, Type);
    }
}
