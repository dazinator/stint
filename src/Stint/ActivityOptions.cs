namespace Stint
{
    using System.Collections.Generic;
    using System.Diagnostics;

    public class ActivityOptions
    {
        public ActivityOptions()
        {
        }
        /// <summary>
        /// Tags applied to all activities created by Stint.
        /// </summary>
        public List<KeyValuePair<string, object?>> GlobalTags { get; set; } = new List<KeyValuePair<string, object?>>();
      
    }
}
