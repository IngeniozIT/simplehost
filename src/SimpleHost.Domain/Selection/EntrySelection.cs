using SimpleHost.Domain.Entries;

namespace SimpleHost.Domain.Selection;

/// <summary>
/// An immutable set of host entries the user has picked out of the list, establishing the
/// selection that later bulk actions — enable, disable, delete, move — operate on together.
/// </summary>
/// <remarks>
/// Membership is keyed by an entry's identity: its IP address (compared exactly) and hostname
/// (compared case-insensitively, since DNS resolution is case-insensitive), matching the identity
/// used everywhere else in the domain. An entry's inline comment and active/disabled status are not
/// part of its identity, so selecting, deselecting, or testing membership with an entry that agrees
/// on IP and hostname but differs only in comment or status refers to the same selected entry.
/// Selecting an already-selected entry is a no-op, so an entry never appears twice; entries are kept
/// in the order they were first selected. Every operation returns a new selection, leaving the
/// original untouched.
/// </remarks>
public sealed record EntrySelection
{
    private EntrySelection(IReadOnlyList<HostEntry> entries)
    {
        Entries = entries;
    }

    /// <summary>An empty selection — the starting point before the user has picked anything.</summary>
    public static EntrySelection Empty { get; } = new(Array.Empty<HostEntry>());

    /// <summary>The selected entries, in the order they were first selected.</summary>
    public IReadOnlyList<HostEntry> Entries { get; }

    /// <summary>How many entries are currently selected.</summary>
    public int Count => Entries.Count;

    /// <summary>Whether nothing is currently selected.</summary>
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>
    /// Whether <paramref name="entry"/> is part of the selection, matched by identity (IP address
    /// exactly, hostname case-insensitively; comment and status ignored).
    /// </summary>
    public bool Contains(HostEntry entry) => Entries.Any(selected => SameIdentity(selected, entry));

    /// <summary>
    /// Returns the selection with <paramref name="entry"/> added. Adding an entry already in the
    /// selection is a no-op, so an entry is never selected twice.
    /// </summary>
    public EntrySelection Select(HostEntry entry)
    {
        if (Contains(entry))
        {
            return this;
        }

        return new EntrySelection([.. Entries, entry]);
    }

    /// <summary>
    /// Returns the selection with the entry matching <paramref name="entry"/>'s identity removed.
    /// Deselecting an entry that is not selected is a no-op.
    /// </summary>
    public EntrySelection Deselect(HostEntry entry)
    {
        if (!Contains(entry))
        {
            return this;
        }

        return new EntrySelection([.. Entries.Where(selected => !SameIdentity(selected, entry))]);
    }

    /// <summary>
    /// Returns the selection with <paramref name="entry"/> deselected if it was selected, or selected
    /// if it was not — the natural click-to-toggle interaction of a multi-select list.
    /// </summary>
    public EntrySelection Toggle(HostEntry entry)
        => Contains(entry) ? Deselect(entry) : Select(entry);

    /// <summary>Returns an empty selection, dropping everything currently selected.</summary>
    public EntrySelection Clear() => Empty;

    private static bool SameIdentity(HostEntry left, HostEntry right)
        => string.Equals(left.IpAddress, right.IpAddress, StringComparison.Ordinal)
            && string.Equals(left.Hostname, right.Hostname, StringComparison.OrdinalIgnoreCase);
}
