namespace Jellyfin.MovieRequests.Services;

public sealed class TMDbNotConfiguredException : Exception
{
    public TMDbNotConfiguredException()
        : base("The TMDb API key is not configured.")
    {
    }
}

public sealed class TMDbServiceException : Exception
{
    public TMDbServiceException(string message, bool isRateLimited = false, Exception? innerException = null)
        : base(message, innerException)
    {
        IsRateLimited = isRateLimited;
    }

    public bool IsRateLimited { get; }
}
