using MediaBrowser.Model.Plugins;

namespace Jellyfin.MovieRequests.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public bool EnableVoting { get; set; } = true;

    public int MaxRequestsPerUser { get; set; } = 20;

    public string TMDbReadAccessToken { get; set; } = string.Empty;
}