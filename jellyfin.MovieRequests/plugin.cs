using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.MovieRequests;

public class Plugin : BasePlugin<Configuration.PluginConfiguration>, IHasWebPages
{
    public static Plugin? Instance { get; private set; }

    public override string Name => "Movie Requests";

    public override Guid Id =>
        Guid.Parse("6f566df7-b7f2-4fac-9034-7ecf6a900001");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath =
                    $"{GetType().Namespace}.Web.configPage.html"
            },
            new PluginPageInfo
            {
                Name = "movieRequests",
                EmbeddedResourcePath =
                    $"{GetType().Namespace}.Web.movieRequests.html"
            }
        };
    }
}