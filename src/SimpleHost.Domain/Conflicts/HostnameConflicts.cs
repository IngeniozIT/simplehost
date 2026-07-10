using System.Net;
using System.Net.Sockets;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Domain.Conflicts;

/// <summary>
/// The outcome of scanning a set of entries for hostnames that resolve ambiguously — that is,
/// hostnames mapped to conflicting IP addresses. Produced so the UI can warn the user about
/// conflicts that need resolving.
/// </summary>
/// <remarks>
/// Hostnames are grouped case-insensitively (DNS resolution is case-insensitive), while IP
/// addresses are compared exactly. A hostname mapped to a single IP address — even when repeated
/// across several identical entries — is not a conflict. Two or more distinct IP addresses are a
/// conflict only when at least two of them share an address family: a single IPv4 address paired
/// with a single IPv6 address is the normal dual-stack case (for example <c>localhost</c> at
/// <c>127.0.0.1</c> and <c>::1</c>) and is not reported. Conflicts are listed in the order their
/// hostnames are first seen, and each conflict lists all the mapped IP addresses in first-seen
/// order.
/// </remarks>
public sealed record HostnameConflicts
{
    private HostnameConflicts(IReadOnlyList<HostnameConflict> conflicts)
    {
        Conflicts = conflicts;
    }

    /// <summary>
    /// The detected conflicts, in the order their hostnames are first seen; empty when no hostname
    /// resolves ambiguously.
    /// </summary>
    public IReadOnlyList<HostnameConflict> Conflicts { get; }

    /// <summary>
    /// Whether any hostname is mapped to conflicting IP addresses — two or more distinct addresses
    /// of the same family.
    /// </summary>
    public bool HasConflicts => Conflicts.Count > 0;

    /// <summary>
    /// Scans <paramref name="entries"/> for hostnames mapped to conflicting IP addresses — two or
    /// more distinct addresses of the same family (a lone IPv4/IPv6 pair is not a conflict).
    /// </summary>
    /// <param name="entries">The entries to scan, in file order.</param>
    /// <returns>
    /// A report listing one <see cref="HostnameConflict"/> per hostname that resolves to conflicting
    /// IP addresses; an empty report when every hostname is consistent.
    /// </returns>
    public static HostnameConflicts Of(IEnumerable<HostEntry> entries)
    {
        var order = new List<string>();
        var buckets = new Dictionary<string, Bucket>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (!buckets.TryGetValue(entry.Hostname, out var bucket))
            {
                bucket = new Bucket(entry.Hostname);
                buckets[entry.Hostname] = bucket;
                order.Add(entry.Hostname);
            }

            if (!bucket.IpAddresses.Contains(entry.IpAddress))
            {
                bucket.IpAddresses.Add(entry.IpAddress);
            }
        }

        var conflicts = new List<HostnameConflict>();
        foreach (var key in order)
        {
            var bucket = buckets[key];
            if (HasSameFamilyConflict(bucket.IpAddresses))
            {
                conflicts.Add(HostnameConflict.For(bucket.Hostname, bucket.IpAddresses));
            }
        }

        return new HostnameConflicts(conflicts);
    }

    /// <summary>
    /// Whether the distinct IP addresses a single hostname maps to include two or more of the same
    /// address family. A lone IPv4 address alongside a lone IPv6 address (the dual-stack case) is
    /// not a conflict; a second address of a family already seen is.
    /// </summary>
    private static bool HasSameFamilyConflict(IReadOnlyList<string> distinctIpAddresses)
    {
        var seenFamilies = new HashSet<AddressFamily>();
        foreach (var ipAddress in distinctIpAddresses)
        {
            if (!seenFamilies.Add(FamilyOf(ipAddress)))
            {
                return true;
            }
        }

        return false;
    }

    private static AddressFamily FamilyOf(string ipAddress) =>
        IPAddress.TryParse(ipAddress, out var parsed)
            ? parsed.AddressFamily
            : AddressFamily.Unknown;

    private sealed class Bucket(string hostname)
    {
        public string Hostname { get; } = hostname;

        public List<string> IpAddresses { get; } = new();
    }
}
