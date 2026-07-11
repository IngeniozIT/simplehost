using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Ports;

/// <summary>
/// Port giving the application the ability to fetch a standalone <c>hosts</c> list — such as a public
/// ad-block list — so its entries can be placed into a subscription's own category. The source may be
/// a remote list served at a URL or a local file on disk, so a subscription can be fed either from the
/// network or from a file another tool produces. The broader counterpart of <see cref="IHostsFileImporter"/>:
/// where the importer reads a file the user picked from disk once, this retrieves a list — remote or
/// local — on demand and turns it into the entries it holds. Implemented by infrastructure adapters
/// that know how to retrieve the list and parse the on-disk <c>hosts</c> format.
/// </summary>
public interface IRemoteHostsListFetcher
{
    /// <summary>
    /// Fetches the <c>hosts</c> list at <paramref name="source"/> and returns every entry it holds —
    /// active and disabled (commented-out) alike — in list order, each carrying its
    /// <see cref="HostEntry.Status"/> and any trailing inline comment. Category markers and any other
    /// non-entry lines (blanks, plain comments) are ignored, so the result is a single flat list of
    /// entries regardless of how the list was organised. When <paramref name="source"/> is a local file
    /// path with no file behind it yet, no entries are returned rather than the fetch failing, so a
    /// subscription can be created before its file is produced.
    /// </summary>
    /// <param name="source">
    /// The URL of the remote <c>hosts</c> list to fetch, or the path of a local <c>hosts</c> file to
    /// read.
    /// </param>
    /// <returns>The entries found in the fetched list, in the order they appear.</returns>
    IReadOnlyList<HostEntry> Fetch(string source);
}
