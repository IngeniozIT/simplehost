namespace SimpleHost.Domain.Entries;

/// <summary>
/// A single active host mapping as it appears in the <c>hosts</c> file:
/// an IP address resolving a hostname, with an optional trailing inline comment.
/// </summary>
/// <param name="IpAddress">The IP address the hostname resolves to.</param>
/// <param name="Hostname">The hostname being mapped.</param>
/// <param name="Comment">
/// The trailing inline comment (the text after <c>#</c> on the entry's line), trimmed of
/// surrounding whitespace, or <c>null</c> when the entry carries no inline comment.
/// </param>
public sealed record HostEntry(string IpAddress, string Hostname, string? Comment = null);
