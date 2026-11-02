namespace Jellyfin.MovieRequests.Models;

public class MovieRequest
{
    public Guid Id { get; set; }

    public int TMDbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int? Year { get; set; }

    public string? PosterUrl { get; set; }

    public string RequestedBy { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public string? Username { get; set; }

    public DateTimeOffset Created { get; set; }

    public int Votes { get; set; }

    public bool IsCompleted { get; set; }

    public bool VotedByCurrentUser { get; set; }
}