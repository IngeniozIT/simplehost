namespace SimpleHost.Domain.Entries;

/// <summary>
/// A single active host mapping as it appears in the <c>hosts</c> file:
/// an IP address resolving a hostname.
/// </summary>
public sealed record HostEntry(string IpAddress, string Hostname);
