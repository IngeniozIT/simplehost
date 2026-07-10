using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Ports;

/// <summary>
/// Port giving the application access to the entries stored in the <c>hosts</c> file.
/// Implemented by infrastructure adapters that know the on-disk format.
/// </summary>
public interface IHostsFileRepository
{
    /// <summary>
    /// Returns every active (uncommented) entry currently in the <c>hosts</c> file,
    /// in the order they appear.
    /// </summary>
    IReadOnlyList<HostEntry> GetActiveEntries();
}
