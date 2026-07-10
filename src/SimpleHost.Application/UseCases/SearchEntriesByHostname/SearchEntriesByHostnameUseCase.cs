using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.SearchEntriesByHostname;

/// <summary>
/// Narrows the displayed host entries — active and disabled alike, in file order — to those
/// whose <see cref="HostEntry.Hostname"/> matches the user's search query, so a user can
/// quickly find an entry by typing part of its hostname.
/// </summary>
/// <remarks>
/// Matching is a case-insensitive substring test against the hostname only (never the IP
/// address or inline comment). The query is trimmed of surrounding whitespace before
/// matching; a blank query (empty or whitespace only) narrows nothing and returns every
/// entry, mirroring an emptied search box.
/// </remarks>
public sealed class SearchEntriesByHostnameUseCase(IHostsFileRepository repository)
{
    public IReadOnlyList<HostEntry> Execute(string query)
    {
        // A blank query trims to the empty string, which every hostname "contains", so the
        // filter naturally returns every entry — no special-casing needed.
        var trimmedQuery = query.Trim();

        return repository.GetEntries()
            .Where(entry => entry.Hostname.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
