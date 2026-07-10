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
/// app can request elevation and let the user retry. When the user has opted in, a successful save
/// that wrote changes is followed by a flush of the system DNS cache so the changes take effect
/// immediately.
/// </summary>
public sealed class ApplyPendingChangesUseCase(
    IHostsFileRepository repository,
    IElevationService elevation,
    IDnsCacheFlusher dnsCacheFlusher)
{
    /// <summary>
    /// Writes each change in <paramref name="pendingChanges"/> to the file through the repository,
    /// in recorded order, and clears the set once they are all applied. Applying an empty set writes
    /// nothing and leaves the file unchanged, without requiring elevation. When there are changes to
    /// write but the process is not elevated, nothing is written, the changes stay pending, and
    /// <see cref="ElevationRequiredException"/> is thrown. When <paramref name="flushDnsAfterSave"/>
    /// is <c>true</c> and the save actually wrote changes, the system DNS cache is flushed once,
    /// after every change has been written, so hostname changes take effect without a manual step;
    /// an empty save (nothing written) and a refused save (elevation required) never flush.
    /// </summary>
    /// <param name="pendingChanges">The edits the user has made but not yet written.</param>
    /// <param name="flushDnsAfterSave">
    /// <c>true</c> to flush the system DNS cache after a successful save that wrote changes;
    /// <c>false</c> to leave the cache untouched. Carries the user's opt-in setting.
    /// </param>
    /// <exception cref="ElevationRequiredException">
    /// Thrown when changes are pending but the process lacks administrator rights to write the file.
    /// </exception>
    public void Execute(PendingChangeSet pendingChanges, bool flushDnsAfterSave)
    {
        var hadChanges = pendingChanges.HasChanges;

        if (hadChanges && !elevation.IsElevated)
        {
            throw new ElevationRequiredException();
        }

        foreach (var change in pendingChanges.Changes)
        {
            try
            {
                change.WriteTo(repository);
            }
            catch (InvalidOperationException)
            {
                // A stale change whose target an earlier change already removed can no longer be
                // located on disk. The projection previewed the same change as a no-op, so skip it
                // here too: apply stays consistent with the preview and never crashes mid-write.
            }
        }

        pendingChanges.Clear();

        if (flushDnsAfterSave && hadChanges)
        {
            dnsCacheFlusher.Flush();
        }
    }
}
