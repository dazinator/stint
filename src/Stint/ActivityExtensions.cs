using System;
using System.Diagnostics;

public static class ActivityExtensions
{
    public static void RecordException(this Activity activity, Exception exception)
    {
        if (activity == null || exception == null)
        {
            return;
        }

        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("exception.message", exception.Message);
        activity.SetTag("exception.stacktrace", exception.StackTrace);
    }
}
