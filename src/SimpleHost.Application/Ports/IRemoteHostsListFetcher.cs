using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Ports;

/// <summary>
/// Port giving the application the ability to fetch a remote, standalone <c>hosts</c> list from a
/// URL — such as a public ad-block list — so its entries can be placed into a subscription's own
/// category. The network counterpart of <see cref="IHostsFileImporter"/>: where the importer reads
/// a file the user picked from disk, this fetches a list served over the network and turns it into
/// the entries it holds. Implemented by infrastructure adapters that know how to retrieve the list
/// and parse the on-disk <c>hosts</c> format.
/// </summary>
public interface IRemoteHostsListFetcher
{
    /// <summary>
    /// Fetches the remote <c>hosts</c> list at <paramref name="url"/> and returns every entry it
    /// holds — active and disabled (commented-out) alike — in list order, each carrying its
    /// <see cref="HostEntry.Status"/> and any trailing inline comment. Category markers and any
    /// other non-entry lines (blanks, plain comments) in the fetched list are ignored, so the
    /// result is a single flat list of entries regardless of how the remote list was organised.
    /// </summary>
    /// <param name="url">The URL of the remote <c>hosts</c> list to fetch.</param>
    /// <returns>The entries found in the fetched list, in the order they appear.</returns>
    IReadOnlyList<HostEntry> Fetch(string url);
}
