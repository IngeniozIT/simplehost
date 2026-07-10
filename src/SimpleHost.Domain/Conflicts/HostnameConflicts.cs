using SimpleHost.Domain.Entries;

namespace SimpleHost.Domain.Conflicts;

/// <summary>
/// The outcome of scanning a set of entries for hostnames that resolve ambiguously — that is,
/// hostnames mapped to more than one distinct IP address. Produced so the UI can warn the user
/// about conflicts that need resolving.
/// </summary>
/// <remarks>
/// Hostnames are grouped case-insensitively (DNS resolution is case-insensitive), while IP
/// addresses are compared exactly. A hostname mapped to a single IP address — even when repeated
/// across several identical entries — is not a conflict; only two or more distinct IP addresses for
/// the same hostname are. Conflicts are reported in the order their hostnames are first seen, and
/// each conflict lists its IP addresses in first-seen order.
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
    /// Whether any hostname is mapped to more than one distinct IP address.
    /// </summary>
    public bool HasConflicts => Conflicts.Count > 0;

    /// <summary>
    /// Scans <paramref name="entries"/> for hostnames mapped to more than one distinct IP address.
    /// </summary>
    /// <param name="entries">The entries to scan, in file order.</param>
    /// <returns>
    /// A report listing one <see cref="HostnameConflict"/> per hostname that resolves to two or more
    /// distinct IP addresses; an empty report when every hostname is consistent.
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
            if (bucket.IpAddresses.Count > 1)
            {
                conflicts.Add(HostnameConflict.For(bucket.Hostname, bucket.IpAddresses));
            }
        }

        return new HostnameConflicts(conflicts);
    }

    private sealed class Bucket(string hostname)
    {
        public string Hostname { get; } = hostname;

        public List<string> IpAddresses { get; } = new();
    }
}
