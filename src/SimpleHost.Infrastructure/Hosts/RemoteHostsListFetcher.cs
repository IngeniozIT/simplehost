using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Fetches a standalone <c>hosts</c> list and returns the entries it holds, so a subscription can be
/// populated from it. The source may be a remote list served over HTTP(S) or a local file on disk:
/// an <c>http</c>/<c>https</c> URL is downloaded, anything else is read as a file path. Either way the
/// text is parsed with the same <see cref="HostsFileParser"/> the managed <c>hosts</c> file is read
/// with — the network-and-file counterpart of <see cref="HostsFileImporter"/> — so any valid
/// <c>hosts</c> list, whether or not it carries SimpleHost category markers, turns into the entries it
/// represents.
/// </summary>
/// <remarks>
/// A local file path is accepted even when the file does not exist yet, so a subscription can be set
/// up before its file is produced: reading a path with no file behind it yields no entries rather than
/// failing, and a later fetch reads the entries once the file appears. A remote source, by contrast,
/// still surfaces transport failures (an unreachable host or a non-success status) as exceptions, so a
/// mistyped URL is reported rather than silently treated as empty.
/// </remarks>
public sealed class RemoteHostsListFetcher(HttpClient httpClient, HostsFileParser parser) : IRemoteHostsListFetcher
{
    /// <summary>
    /// Fetches the list at <paramref name="source"/> and parses every entry it holds — active and
    /// disabled alike — in list order. Category markers, blank lines, and plain comments are ignored,
    /// so the result is a flat list of the list's entries. An <c>http</c>/<c>https</c> URL is
    /// downloaded; any other value is read as a local file path, which yields no entries when the file
    /// does not exist yet.
    /// </summary>
    /// <param name="source">
    /// The <c>http</c>/<c>https</c> URL of the remote <c>hosts</c> list, or the path of a local
    /// <c>hosts</c> file to read.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="source"/> is <c>null</c>, empty, or whitespace only.
    /// </exception>
    public IReadOnlyList<HostEntry> Fetch(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var content = IsHttpUrl(source) ? DownloadFrom(source) : ReadFileAt(source);
        return content is null ? [] : parser.ParseEntries(content);
    }

    private string DownloadFrom(string url) => httpClient.GetStringAsync(url).GetAwaiter().GetResult();

    private static string? ReadFileAt(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static bool IsHttpUrl(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
