namespace SimpleHost.Domain.Preview;

/// <summary>
/// One line of a pending-changes preview: an entry placement together with how it changes
/// between the current on-disk file and the projected result. Unchanged lines are retained so
/// the preview reads as a diff — added and removed lines shown in context, not in isolation.
/// </summary>
/// <param name="Kind">Whether the placement is unchanged, added, or removed.</param>
/// <param name="Placement">The entry and the category it sits under on this line.</param>
public sealed record HostsFileDiffLine(HostsFileDiffLineKind Kind, HostEntryPlacement Placement);
