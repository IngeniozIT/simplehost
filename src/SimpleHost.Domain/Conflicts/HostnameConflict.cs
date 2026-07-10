namespace SimpleHost.Domain.Conflicts;

/// <summary>
/// A single hostname that is mapped to more than one distinct IP address across the entries, so it
/// resolves ambiguously. Carries the offending hostname, the conflicting IP addresses in the order
/// they were first seen, and a human-readable message the UI can surface as a warning.
/// </summary>
public sealed record HostnameConflict
{
    private HostnameConflict(string hostname, IReadOnlyList<string> ipAddresses, string message)
    {
        Hostname = hostname;
        IpAddresses = ipAddresses;
        Message = message;
    }

    /// <summary>
    /// The hostname that is mapped to conflicting IP addresses, in the spelling first seen in the
    /// entries (hostnames are matched case-insensitively).
    /// </summary>
    public string Hostname { get; }

    /// <summary>
    /// The distinct IP addresses the hostname is mapped to, in the order they were first seen. A
    /// conflict always carries at least two.
    /// </summary>
    public IReadOnlyList<string> IpAddresses { get; }

    /// <summary>
    /// A human-readable warning naming the hostname and the conflicting IP addresses.
    /// </summary>
    public string Message { get; }

    internal static HostnameConflict For(string hostname, IReadOnlyList<string> ipAddresses) =>
        new(
            hostname,
            ipAddresses,
            $"'{hostname}' is mapped to conflicting IP addresses: {string.Join(", ", ipAddresses)}.");
}
