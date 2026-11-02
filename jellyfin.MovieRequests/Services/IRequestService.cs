using Jellyfin.MovieRequests.Models;

namespace Jellyfin.MovieRequests.Services;

public interface IRequestService
{
    IReadOnlyList<MovieRequest> GetAll(Guid? currentUserId = null);

    MovieRequest? GetById(Guid requestId);

    void Add(MovieRequest request);

    bool HasRequestForTmDbId(int tmDbId);

    bool AddVote(Guid requestId, Guid userId, string username);

    bool RemoveVote(Guid requestId, Guid userId);

    bool SetCompleted(Guid requestId, bool isCompleted);

    bool Delete(Guid requestId);
}
