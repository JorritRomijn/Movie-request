using Jellyfin.MovieRequests.Api;

namespace Jellyfin.MovieRequests.Services;

public interface ITMDbService
{
    Task<IReadOnlyList<MovieSearchResultResponse>> SearchMoviesAsync(
        string query,
        CancellationToken cancellationToken);

    Task<MovieSearchResultResponse?> GetMovieAsync(
        int tmDbId,
        CancellationToken cancellationToken);
}
