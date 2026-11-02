namespace Jellyfin.MovieRequests.Services;

public sealed record ExistingLibraryMovie(string Title, int? Year);

public interface ILibraryMovieLookup
{
    ExistingLibraryMovie? FindMovie(string title, int? year, CancellationToken cancellationToken);
}
