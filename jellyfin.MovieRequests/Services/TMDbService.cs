using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Jellyfin.MovieRequests.Api;
using Microsoft.Extensions.Configuration;

namespace Jellyfin.MovieRequests.Services;

public sealed class TMDbService : ITMDbService
{
    private const string HttpClientName = "TMDb";
    private const string ApiKeySetting = "TMDB_API_READ_ACCESS_TOKEN";
    private const string ImageBaseUrl = "https://image.tmdb.org/t/p/w500/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public TMDbService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<MovieSearchResultResponse>> SearchMoviesAsync(
        string query,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new TMDbNotConfiguredException();
        }

        var requestUri = string.Concat(
            "search/movie?query=",
            Uri.EscapeDataString(query.Trim()),
            "&include_adult=false");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.ParseAdd("application/json");
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new TMDbServiceException(
                    $"TMDb returned HTTP {(int)response.StatusCode}.",
                    response.StatusCode == HttpStatusCode.TooManyRequests);
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
            return ParseResults(document.RootElement);
        }
        catch (HttpRequestException exception)
        {
            throw new TMDbServiceException("TMDb could not be reached.", innerException: exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TMDbServiceException("TMDb search timed out.", innerException: exception);
        }
        catch (JsonException exception)
        {
            throw new TMDbServiceException("TMDb returned an invalid search response.", innerException: exception);
        }
    }

    public async Task<MovieSearchResultResponse?> GetMovieAsync(
        int tmDbId,
        CancellationToken cancellationToken)
    {
        if (tmDbId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tmDbId));
        }

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new TMDbNotConfiguredException();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"movie/{tmDbId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.ParseAdd("application/json");
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new TMDbServiceException(
                    $"TMDb returned HTTP {(int)response.StatusCode}.",
                    response.StatusCode == HttpStatusCode.TooManyRequests);
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
            return ParseMovie(document.RootElement)
                ?? throw new TMDbServiceException("TMDb returned an incomplete movie response.");
        }
        catch (HttpRequestException exception)
        {
            throw new TMDbServiceException("TMDb could not be reached.", innerException: exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TMDbServiceException("TMDb search timed out.", innerException: exception);
        }
        catch (JsonException exception)
        {
            throw new TMDbServiceException("TMDb returned an invalid movie response.", innerException: exception);
        }
    }

    private static IReadOnlyList<MovieSearchResultResponse> ParseResults(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            throw new TMDbServiceException("TMDb returned an unexpected search response.");
        }

        var movies = new List<MovieSearchResultResponse>();
        foreach (var result in results.EnumerateArray())
        {
            var movie = ParseMovie(result);
            if (movie is not null)
            {
                movies.Add(movie);
            }
        }

        return movies;
    }

    private string? GetApiKey()
    {
        var configuredToken = Plugin.Instance?.Configuration.TMDbReadAccessToken;
        return string.IsNullOrWhiteSpace(configuredToken)
            ? _configuration[ApiKeySetting]
            : configuredToken.Trim();
    }

    private static MovieSearchResultResponse? ParseMovie(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object
            || !result.TryGetProperty("id", out var idElement)
            || !idElement.TryGetInt32(out var tmDbId)
            || !result.TryGetProperty("title", out var titleElement)
            || titleElement.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(titleElement.GetString()))
        {
            return null;
        }

        var releaseDate = result.TryGetProperty("release_date", out var dateElement)
            && dateElement.ValueKind == JsonValueKind.String
                ? dateElement.GetString()
                : null;
        var year = DateOnly.TryParseExact(
            releaseDate,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var release)
                ? release.Year
                : (int?)null;

        var posterPath = result.TryGetProperty("poster_path", out var posterElement)
            && posterElement.ValueKind == JsonValueKind.String
                ? posterElement.GetString()
                : null;
        var posterUrl = string.IsNullOrWhiteSpace(posterPath)
            ? null
            : string.Concat(ImageBaseUrl, Uri.EscapeDataString(posterPath.TrimStart('/')));

        return new MovieSearchResultResponse(tmDbId, titleElement.GetString()!, year, posterUrl);
    }
}
