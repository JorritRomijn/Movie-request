namespace Jellyfin.MovieRequests.Api;

/// <summary>Movie request information returned by the API.</summary>
public sealed record MovieRequestResponse(
    Guid Id,
    int TMDbId,
    string Title,
    int? Year,
    string? PosterUrl,
    string RequestedBy,
    Guid? UserId,
    string? Username,
    DateTimeOffset Created,
    int Votes,
    bool IsCompleted,
    bool VotedByCurrentUser);
