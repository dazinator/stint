namespace Stint
{
    using System;

    //Define event argument you want to send while raising event.
    public class JobCompletedEventArgs : EventArgs
    {
        public string Name { get; set; }

        public JobCompletedEventArgs(string jobName) => Name = jobName;
    }

}
