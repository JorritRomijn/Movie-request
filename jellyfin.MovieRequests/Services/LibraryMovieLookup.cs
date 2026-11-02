using System.Globalization;
using System.Text;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.MovieRequests.Services;

public sealed class LibraryMovieLookup : ILibraryMovieLookup
{
    private readonly ILibraryManager _libraryManager;

    public LibraryMovieLookup(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    public ExistingLibraryMovie? FindMovie(
        string title,
        int? year,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedTitle = NormalizeTitle(title);
        var movies = _libraryManager.GetItemList(new InternalItemsQuery
        {
            SearchTerm = title,
            IncludeItemTypes = [BaseItemKind.Movie],
            Recursive = true
        });

        cancellationToken.ThrowIfCancellationRequested();
        foreach (var item in movies)
        {
            if (!string.Equals(NormalizeTitle(item.Name), normalizedTitle, StringComparison.Ordinal))
            {
                continue;
            }

            var itemYear = item.ProductionYear;
            if (year.HasValue && itemYear.HasValue && year.Value != itemYear.Value)
            {
                continue;
            }

            return new ExistingLibraryMovie(item.Name, itemYear);
        }

        return null;
    }

    private static string NormalizeTitle(string title)
    {
        var normalized = title.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }
}
