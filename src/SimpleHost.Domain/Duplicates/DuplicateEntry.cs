namespace SimpleHost.Domain.Duplicates;

/// <summary>
/// A single entry — one IP address and hostname pairing — that appears more than once across the
/// scanned entries, so it is redundant. Carries the offending IP address and hostname (in the
/// spelling first seen), how many times the pairing occurs, and a human-readable message the UI can
/// surface as a warning.
/// </summary>
public sealed record DuplicateEntry
{
    private DuplicateEntry(string ipAddress, string hostname, int count, string message)
    {
        IpAddress = ipAddress;
        Hostname = hostname;
        Count = count;
        Message = message;
    }

    /// <summary>
    /// The IP address of the duplicated entry, in the spelling first seen in the entries (IP
    /// addresses are matched exactly).
    /// </summary>
    public string IpAddress { get; }

    /// <summary>
    /// The hostname of the duplicated entry, in the spelling first seen in the entries (hostnames
    /// are matched case-insensitively).
    /// </summary>
    public string Hostname { get; }

    /// <summary>
    /// How many times the IP-address/hostname pairing occurs. A duplicate always carries at least
    /// two.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// A human-readable warning naming the duplicated entry and how many times it appears.
    /// </summary>
    public string Message { get; }

    internal static DuplicateEntry For(string ipAddress, string hostname, int count) =>
        new(
            ipAddress,
            hostname,
            count,
            $"'{ipAddress} {hostname}' is duplicated: it appears {count} times.");
}
