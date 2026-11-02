using System.Security.Claims;
using Jellyfin.MovieRequests.Models;
using Jellyfin.MovieRequests.Services;
using Microsoft.Extensions.Logging;
using MediaBrowser.Controller.Library;
using Jellyfin.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.MovieRequests.Api;

[ApiController]
[Route("MovieRequests")]
public sealed class RequestController : ControllerBase
{
    private const string WebResourcePrefix = "Jellyfin.MovieRequests.Web.";
    private readonly IRequestService _requestService;
    private readonly ITMDbService _tmDbService;
    private readonly ILibraryMovieLookup _libraryMovieLookup;
    private readonly IUserManager _userManager;
    private readonly ILogger<RequestController> _logger;

    public RequestController(
        IRequestService requestService,
        ITMDbService tmDbService,
        ILibraryMovieLookup libraryMovieLookup,
        IUserManager userManager,
        ILogger<RequestController> logger)
    {
        _requestService = requestService;
        _tmDbService = tmDbService;
        _libraryMovieLookup = libraryMovieLookup;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("ui.js")]
    [Produces("text/javascript")]
    public IActionResult GetWebScript()
    {
        return GetWebResource($"{WebResourcePrefix}movieRequests.js", "text/javascript; charset=utf-8");
    }

    [HttpGet("ui.css")]
    [Produces("text/css")]
    public IActionResult GetWebStylesheet()
    {
        return GetWebResource($"{WebResourcePrefix}movieRequests.css", "text/css; charset=utf-8");
    }

    [HttpGet("search")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<MovieSearchResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<MovieSearchResultResponse>>> Search(
        [FromQuery] SearchMoviesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await _tmDbService.SearchMoviesAsync(request.Query.Trim(), cancellationToken);
            return Ok(results);
        }
        catch (TMDbNotConfiguredException exception)
        {
            _logger.LogError(exception, "TMDb movie search is not configured.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Movie search is unavailable",
                detail: "The server administrator has not configured TMDb movie search.");
        }
        catch (TMDbServiceException exception)
        {
            _logger.LogWarning(exception, "TMDb movie search failed.");
            return Problem(
                statusCode: exception.IsRateLimited
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status502BadGateway,
                title: "Movie search provider unavailable",
                detail: "TMDb could not complete the movie search. Please try again later.");
        }
    }

    [HttpGet("list")]
    [ProducesResponseType(typeof(IReadOnlyList<MovieRequestResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<MovieRequestResponse>> GetAll(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var currentUser = GetCurrentUser();
        var requests = _requestService.GetAll(currentUser?.Id)
            .Select(request => ToResponse(request, request.VotedByCurrentUser))
            .ToArray();
        return Ok(requests);
    }

    [HttpGet("capabilities")]
    [Authorize]
    [ProducesResponseType(typeof(MovieRequestCapabilitiesResponse), StatusCodes.Status200OK)]
    public ActionResult<MovieRequestCapabilitiesResponse> GetCapabilities()
    {
        var currentUser = GetCurrentUser();
        return currentUser is null
            ? Unauthorized()
            : Ok(new MovieRequestCapabilitiesResponse(IsAdministrator(currentUser)));
    }

    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [ProducesResponseType(typeof(MovieRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<MovieRequestResponse> GetById(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = _requestService.GetById(id);
        return request is null ? NotFound() : Ok(ToResponse(request));
    }

    [HttpPost("request")]
    [Authorize]
    [ProducesResponseType(typeof(MovieRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MovieRequestResponse>> Create(
        [FromBody] CreateMovieRequest input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var currentUser = GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized();
        }

        MovieSearchResultResponse? movie;
        try
        {
            movie = await _tmDbService.GetMovieAsync(input.TMDbId, cancellationToken);
        }
        catch (TMDbNotConfiguredException exception)
        {
            _logger.LogError(exception, "TMDb movie search is not configured.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Movie request is unavailable",
                detail: "The server administrator has not configured TMDb movie search.");
        }
        catch (TMDbServiceException exception)
        {
            _logger.LogWarning(exception, "TMDb movie details lookup failed for {TMDbId}.", input.TMDbId);
            return Problem(
                statusCode: exception.IsRateLimited
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status502BadGateway,
                title: "Movie metadata provider unavailable",
                detail: "TMDb could not verify the selected movie. Please try again later.");
        }

        if (movie is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Unknown TMDb movie",
                Detail = "The selected TMDb movie does not exist."
            });
        }

        if (_requestService.HasRequestForTmDbId(movie.TMDbId))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Movie already requested",
                Detail = "A request for this movie already exists."
            });
        }

        var existingMovie = _libraryMovieLookup.FindMovie(movie.Title, movie.Year, cancellationToken);
        if (existingMovie is not null)
        {
            var year = existingMovie.Year.HasValue ? $" ({existingMovie.Year.Value})" : string.Empty;
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Movie already in library",
                Detail = "This movie is already available in Jellyfin"
            };
            problem.Extensions["existingMovie"] = new
            {
                existingMovie.Title,
                existingMovie.Year
            };
            return Conflict(problem);
        }

        var request = new MovieRequest
        {
            Id = Guid.NewGuid(),
            TMDbId = movie.TMDbId,
            Title = movie.Title,
            Year = movie.Year,
            PosterUrl = movie.PosterUrl,
            RequestedBy = currentUser.Username,
            UserId = currentUser.Id,
            Username = currentUser.Username,
            Created = DateTimeOffset.UtcNow
        };

        _requestService.Add(request);
        var response = ToResponse(request);
        return CreatedAtAction(nameof(GetById), new { id = request.Id }, response);
    }

    [HttpPost("vote/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Vote(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var currentUser = GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized();
        }

        var request = _requestService.GetById(id);
        if (request is null)
        {
            return NotFound();
        }

        if (request.IsCompleted)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Request completed",
                Detail = "Voting is closed for completed requests."
            });
        }

        return _requestService.AddVote(id, currentUser.Id, currentUser.Username)
            ? NoContent()
            : Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Already voted",
                Detail = "This user has already voted for this request."
            });
    }

    [HttpDelete("request/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DeleteRequest(Guid id)
    {
        if (!IsAdministrator())
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        }

        return _requestService.Delete(id) ? NoContent() : NotFound();
    }

    [HttpPut("request/{id:guid}/completed")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult MarkCompleted(Guid id)
    {
        if (!IsAdministrator())
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        }

        return _requestService.SetCompleted(id, true) ? NoContent() : NotFound();
    }

    private static MovieRequestResponse ToResponse(MovieRequest request, bool votedByCurrentUser = false)
    {
        return new MovieRequestResponse(
            request.Id,
            request.TMDbId,
            request.Title,
            request.Year,
            request.PosterUrl,
            request.RequestedBy,
            request.UserId,
            request.Username,
            request.Created,
            request.Votes,
            request.IsCompleted,
            votedByCurrentUser);
    }

    private bool IsAdministrator()
    {
        var currentUser = GetCurrentUser();
        return currentUser is not null && IsAdministrator(currentUser);
    }

    private bool IsAdministrator(User currentUser)
    {
        return _userManager.GetUserDto(currentUser).Policy.IsAdministrator;
    }

    private IActionResult GetWebResource(string resourceName, string contentType)
    {
        var stream = typeof(Plugin).Assembly.GetManifestResourceStream(resourceName);
        return stream is null ? NotFound() : File(stream, contentType);
    }

    private User? GetCurrentUser()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        return _userManager.GetUserById(userId);
    }
}