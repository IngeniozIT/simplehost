using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Fetches a remote, standalone <c>hosts</c> list over HTTP and returns the entries it holds, so a
/// subscription can be populated from it. The network counterpart of <see cref="HostsFileImporter"/>:
/// it downloads the list's text and parses it with the same <see cref="HostsFileParser"/> the
/// managed <c>hosts</c> file is read with, so any valid <c>hosts</c> list — whether or not it carries
/// SimpleHost category markers — turns into the entries it represents.
/// </summary>
public sealed class RemoteHostsListFetcher(HttpClient httpClient, HostsFileParser parser) : IRemoteHostsListFetcher
{
    /// <summary>
    /// Fetches the remote list at <paramref name="url"/> and parses every entry it holds — active
    /// and disabled alike — in list order. Category markers, blank lines, and plain comments are
    /// ignored, so the result is a flat list of the list's entries.
    /// </summary>
    /// <param name="url">The URL of the remote <c>hosts</c> list to fetch.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="url"/> is <c>null</c>, empty, or whitespace only.
    /// </exception>
    public IReadOnlyList<HostEntry> Fetch(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var content = httpClient.GetStringAsync(url).GetAwaiter().GetResult();
        return parser.ParseEntries(content);
    }
}
