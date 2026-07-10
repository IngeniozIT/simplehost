using SimpleHost.Application.Pending;
using SimpleHost.Application.Ports;

namespace SimpleHost.Application.UseCases.ApplyPendingChanges;

/// <summary>
/// Applies the user's accumulated pending changes to the <c>hosts</c> file. This is the single
/// explicit write step: until it runs, every edit lives only in the <see cref="PendingChangeSet"/>
/// and the file on disk is untouched. Running it writes all pending changes through the repository,
/// in the order they were made, then empties the set so nothing is left pending. Because the
/// <c>hosts</c> file lives under a protected system location, writing it requires administrator
/// (elevated) rights: when changes are pending but the process is not elevated the step writes
/// nothing, keeps the changes pending, and raises <see cref="ElevationRequiredException"/> so the
/// app can request elevation and let the user retry.
/// </summary>
public sealed class ApplyPendingChangesUseCase(IHostsFileRepository repository, IElevationService elevation)
{
    /// <summary>
    /// Writes each change in <paramref name="pendingChanges"/> to the file through the repository,
    /// in recorded order, and clears the set once they are all applied. Applying an empty set writes
    /// nothing and leaves the file unchanged, without requiring elevation. When there are changes to
    /// write but the process is not elevated, nothing is written, the changes stay pending, and
    /// <see cref="ElevationRequiredException"/> is thrown.
    /// </summary>
    /// <param name="pendingChanges">The edits the user has made but not yet written.</param>
    /// <exception cref="ElevationRequiredException">
    /// Thrown when changes are pending but the process lacks administrator rights to write the file.
    /// </exception>
    public void Execute(PendingChangeSet pendingChanges)
    {
        if (pendingChanges.HasChanges && !elevation.IsElevated)
        {
            throw new ElevationRequiredException();
        }

        foreach (var change in pendingChanges.Changes)
        {
            change.WriteTo(repository);
        }

        pendingChanges.Clear();
    }
}
