namespace osu.Game.Plugins;

public abstract class LoadException : Exception
{
    public LoadException()
    {
    }

    public LoadException(string? message)
        : base(message)
    {
    }

    public LoadException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}