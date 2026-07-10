namespace SimpleHost.Domain.Preview;

/// <summary>
/// How a single line of a pending-changes preview relates the current on-disk <c>hosts</c> file
/// to what it will look like once the pending changes are applied.
/// </summary>
public enum HostsFileDiffLineKind
{
    /// <summary>The entry is present, in the same place, both before and after applying.</summary>
    Unchanged,

    /// <summary>The entry is not on disk yet and will appear once the pending changes are applied.</summary>
    Added,

    /// <summary>The entry is on disk now and will be gone once the pending changes are applied.</summary>
    Removed,
}
