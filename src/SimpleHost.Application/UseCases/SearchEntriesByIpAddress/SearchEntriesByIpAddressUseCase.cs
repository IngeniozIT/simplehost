using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.SearchEntriesByIpAddress;

/// <summary>
/// Narrows the displayed host entries — active and disabled alike, in file order — to those
/// whose <see cref="HostEntry.IpAddress"/> matches the user's search query, so a user can
/// quickly find an entry by typing part of its IP address.
/// </summary>
/// <remarks>
/// Matching is a case-insensitive substring test against the IP address only (never the
/// hostname or inline comment); case-insensitivity lets hexadecimal IPv6 addresses be found
/// regardless of the casing typed. The query is trimmed of surrounding whitespace before
/// matching; a blank query (empty or whitespace only) narrows nothing and returns every
/// entry, mirroring an emptied search box.
/// </remarks>
public sealed class SearchEntriesByIpAddressUseCase(IHostsFileRepository repository)
{
    public IReadOnlyList<HostEntry> Execute(string query)
    {
        // A blank query trims to the empty string, which every IP address "contains", so the
        // filter naturally returns every entry — no special-casing needed.
        var trimmedQuery = query.Trim();

        return repository.GetEntries()
            .Where(entry => entry.IpAddress.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
