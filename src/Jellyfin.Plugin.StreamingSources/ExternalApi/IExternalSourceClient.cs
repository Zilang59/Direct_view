using Jellyfin.Plugin.StreamingSources.Models;

namespace Jellyfin.Plugin.StreamingSources.ExternalApi;

public interface IExternalSourceClient
{
    Task<IReadOnlyList<StreamingSource>> SearchAsync(MediaLookupRequest request, CancellationToken cancellationToken);
}
