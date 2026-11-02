using System.ComponentModel.DataAnnotations;

namespace Jellyfin.MovieRequests.Api;

/// <summary>Payload for submitting a TMDb movie request.</summary>
public sealed record CreateMovieRequest
{
    /// <summary>Identifier of the selected movie in TMDb.</summary>
    [Range(1, int.MaxValue)]
    public int TMDbId { get; init; }
}
