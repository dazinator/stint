namespace Stint
{
    using System;

    public class JobConfig
    {
        public JobConfig() => Triggers = new TriggersConfig();

        public TriggersConfig Triggers { get; set; }

        public string Type { get; set; }

        public override bool Equals(object obj) => Equals(obj as JobConfig);

        public bool Equals(JobConfig obj)
        {
            var equal = obj != null &&
                obj.Type == Type &&
                obj.Triggers.Equals(this.Triggers);
            return equal;
        }
    }
}
