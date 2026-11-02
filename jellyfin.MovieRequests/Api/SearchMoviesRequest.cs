using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.MovieRequests.Api;

/// <summary>Query parameters for searching TMDb movies.</summary>
public sealed record SearchMoviesRequest
{
    /// <summary>Movie title or search terms.</summary>
    [FromQuery(Name = "query")]
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Query { get; init; }
}
