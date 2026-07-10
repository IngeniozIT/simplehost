namespace SimpleHost.Domain.Entries;

/// <summary>
/// Whether a host entry currently resolves or is kept in the <c>hosts</c> file but does not.
/// </summary>
public enum HostEntryStatus
{
    /// <summary>An uncommented entry that actively resolves its hostname.</summary>
    Active,

    /// <summary>
    /// An entry that is commented out with a leading <c>#</c> — kept in place for reference
    /// but not resolving.
    /// </summary>
    Disabled,
}
