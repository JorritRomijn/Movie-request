using Jellyfin.MovieRequests.Models;
using Jellyfin.MovieRequests.Repositories;

namespace Jellyfin.MovieRequests.Services;

public sealed class RequestService : IRequestService
{
    private readonly RequestRepository _repository;

    public RequestService(RequestRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<MovieRequest> GetAll(Guid? currentUserId = null)
    {
        return _repository.GetAll(currentUserId);
    }

    public MovieRequest? GetById(Guid requestId)
    {
        return _repository.GetById(requestId);
    }

    public void Add(MovieRequest request)
    {
        _repository.Add(request);
    }

    public bool HasRequestForTmDbId(int tmDbId)
    {
        return _repository.HasRequestForTmDbId(tmDbId);
    }

    public bool AddVote(Guid requestId, Guid userId, string username)
    {
        return _repository.AddVote(requestId, userId, username);
    }

    public bool RemoveVote(Guid requestId, Guid userId)
    {
        return _repository.RemoveVote(requestId, userId);
    }

    public bool SetCompleted(Guid requestId, bool isCompleted)
    {
        return _repository.SetCompleted(requestId, isCompleted);
    }

    public bool Delete(Guid requestId)
    {
        return _repository.Delete(requestId);
    }
}