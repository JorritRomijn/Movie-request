using System.Text.Json.Serialization;

namespace Jellyfin.MovieRequests.Api;

/// <summary>A movie returned by a TMDb search.</summary>
public sealed record MovieSearchResultResponse(
    [property: JsonPropertyName("TMDbId")]
    int TMDbId,
    string Title,
    int? Year,
    string? PosterUrl);
