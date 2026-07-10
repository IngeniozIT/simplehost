using SimpleHost.Application.Pending;
using SimpleHost.Application.Ports;

namespace SimpleHost.Application.UseCases.ApplyPendingChanges;

/// <summary>
/// Applies the user's accumulated pending changes to the <c>hosts</c> file. This is the single
/// explicit write step: until it runs, every edit lives only in the <see cref="PendingChangeSet"/>
/// and the file on disk is untouched. Running it writes all pending changes through the repository,
/// in the order they were made, then empties the set so nothing is left pending.
/// </summary>
public sealed class ApplyPendingChangesUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Writes each change in <paramref name="pendingChanges"/> to the file through the repository,
    /// in recorded order, and clears the set once they are all applied. Applying an empty set writes
    /// nothing and leaves the file unchanged.
    /// </summary>
    /// <param name="pendingChanges">The edits the user has made but not yet written.</param>
    public void Execute(PendingChangeSet pendingChanges)
    {
        foreach (var change in pendingChanges.Changes)
        {
            change.WriteTo(repository);
        }

        pendingChanges.Clear();
    }
}
