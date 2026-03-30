namespace osu.Plugin.Referee;

public static class Extensions
{
    public static void FireAndForget<T>(this Task<T> task, Action<T>? onSuccess = null, Action<Exception>? onError = null)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                onError?.Invoke(t.Exception);
            }
            else if (t.IsCompletedSuccessfully)
            {
                onSuccess?.Invoke(t.Result);
            }
        }, TaskScheduler.Default);
    }
}