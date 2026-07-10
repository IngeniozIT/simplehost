namespace SimpleHost.Domain.Entries;

/// <summary>
/// A single host mapping as it appears in the <c>hosts</c> file: an IP address resolving a
/// hostname, with an optional trailing inline comment and a status recording whether the
/// entry is active or disabled (commented out).
/// </summary>
/// <param name="IpAddress">The IP address the hostname resolves to.</param>
/// <param name="Hostname">The hostname being mapped.</param>
/// <param name="Comment">
/// The trailing inline comment (the text after <c>#</c> on the entry's line), trimmed of
/// surrounding whitespace, or <c>null</c> when the entry carries no inline comment.
/// </param>
/// <param name="Status">
/// Whether the entry is <see cref="HostEntryStatus.Active"/> (resolving) or
/// <see cref="HostEntryStatus.Disabled"/> (commented out, kept but not resolving).
/// Defaults to <see cref="HostEntryStatus.Active"/>.
/// </param>
public sealed record HostEntry(
    string IpAddress,
    string Hostname,
    string? Comment = null,
    HostEntryStatus Status = HostEntryStatus.Active);
