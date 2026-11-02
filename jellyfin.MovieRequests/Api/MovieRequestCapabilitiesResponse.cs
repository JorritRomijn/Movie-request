namespace Jellyfin.MovieRequests.Api;

/// <summary>Capabilities of the authenticated Jellyfin user.</summary>
public sealed record MovieRequestCapabilitiesResponse(bool IsAdministrator);
