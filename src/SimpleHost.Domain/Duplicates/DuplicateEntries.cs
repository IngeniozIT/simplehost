using SimpleHost.Domain.Entries;

namespace SimpleHost.Domain.Duplicates;

/// <summary>
/// The outcome of scanning a set of entries for duplicates — that is, the same entry (same IP
/// address and hostname) appearing more than once. Produced so the UI can warn the user about
/// redundant entries to clean up.
/// </summary>
/// <remarks>
/// Entries are keyed by their IP address (compared exactly) and hostname (compared
/// case-insensitively, since DNS resolution is case-insensitive); an entry's inline comment and
/// active/disabled status are not part of its identity, so two entries that agree on IP and
/// hostname but differ only in comment or status still count as the same entry. A pairing that
/// appears just once is not a duplicate; only two or more occurrences of the same pairing are.
/// Duplicates are reported in the order their pairing is first seen, each carrying the count of its
/// occurrences.
/// </remarks>
public sealed record DuplicateEntries
{
    private DuplicateEntries(IReadOnlyList<DuplicateEntry> duplicates)
    {
        Duplicates = duplicates;
    }

    /// <summary>
    /// The detected duplicates, in the order their pairing is first seen; empty when no entry is
    /// duplicated.
    /// </summary>
    public IReadOnlyList<DuplicateEntry> Duplicates { get; }

    /// <summary>
    /// Whether any entry (same IP address and hostname) appears more than once.
    /// </summary>
    public bool HasDuplicates => Duplicates.Count > 0;

    /// <summary>
    /// Scans <paramref name="entries"/> for entries whose IP address and hostname pairing appears
    /// more than once.
    /// </summary>
    /// <param name="entries">The entries to scan, in file order.</param>
    /// <returns>
    /// A report listing one <see cref="DuplicateEntry"/> per IP-address/hostname pairing that occurs
    /// two or more times; an empty report when every entry is unique.
    /// </returns>
    public static DuplicateEntries Of(IEnumerable<HostEntry> entries)
    {
        var order = new List<string>();
        var buckets = new Dictionary<string, Bucket>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            var key = $"{entry.IpAddress}\n{entry.Hostname.ToLowerInvariant()}";
            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new Bucket(entry.IpAddress, entry.Hostname);
                buckets[key] = bucket;
                order.Add(key);
            }

            bucket.Count++;
        }

        var duplicates = new List<DuplicateEntry>();
        foreach (var key in order)
        {
            var bucket = buckets[key];
            if (bucket.Count > 1)
            {
                duplicates.Add(DuplicateEntry.For(bucket.IpAddress, bucket.Hostname, bucket.Count));
            }
        }

        return new DuplicateEntries(duplicates);
    }

    private sealed class Bucket(string ipAddress, string hostname)
    {
        public string IpAddress { get; } = ipAddress;

        public string Hostname { get; } = hostname;

        public int Count { get; set; }
    }
}
