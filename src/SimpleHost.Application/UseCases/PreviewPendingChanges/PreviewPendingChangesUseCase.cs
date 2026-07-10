using SimpleHost.Application.Pending;
using SimpleHost.Application.Ports;
using SimpleHost.Domain.Preview;

namespace SimpleHost.Application.UseCases.PreviewPendingChanges;

/// <summary>
/// Previews what the <c>hosts</c> file will look like once the user's accumulated pending changes
/// are applied, without writing anything. Reads the current entries from the repository, projects
/// the pending changes onto them, and returns a diff of the two so the user can review exactly what
/// will change before choosing to apply.
/// </summary>
public sealed class PreviewPendingChangesUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Computes the diff between the current on-disk entries and the result of applying
    /// <paramref name="pendingChanges"/>. When no changes are pending the diff has no additions or
    /// removals, so <see cref="HostsFileDiff.HasChanges"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="pendingChanges">The edits the user has made but not yet written.</param>
    public HostsFileDiff Execute(PendingChangeSet pendingChanges)
    {
        var current = repository.GetGroupedEntries();
        var projected = pendingChanges.ApplyTo(current);

        return HostsFileDiff.Between(
            HostEntryPlacement.Flatten(current),
            HostEntryPlacement.Flatten(projected));
    }
}
