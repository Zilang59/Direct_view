namespace Jellyfin.Plugin.StreamingSources.Debrid;

/// <summary>
/// Contract implemented by Debrid providers.
/// </summary>
public interface IDebridProvider
{
    string Name { get; }

    Task<DebridMagnetResult> AddMagnetAsync(string magnet, CancellationToken cancellationToken);

    Task<IReadOnlyList<DebridFile>> GetFilesAsync(string magnetId, CancellationToken cancellationToken);

    Task<string> GetStreamingUrlAsync(string fileId, CancellationToken cancellationToken);

    Task<bool> IsCachedAsync(string hash, CancellationToken cancellationToken);
}
