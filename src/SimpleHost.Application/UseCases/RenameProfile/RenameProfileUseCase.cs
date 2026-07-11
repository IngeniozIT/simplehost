using SimpleHost.Application.Ports;
using SimpleHost.Domain.Profiles;

namespace SimpleHost.Application.UseCases.RenameProfile;

/// <summary>
/// Renames a previously saved profile, saving it under a new name while keeping everything it records
/// unchanged — a category preset keeps its enabled categories and a snapshot keeps its captured
/// content — so recalling the renamed profile applies or restores exactly what it did before. The
/// rename is persisted through the store, so it survives a restart.
/// </summary>
public sealed class RenameProfileUseCase(IProfileStore store)
{
    /// <summary>
    /// Looks up the saved profile named <paramref name="currentName"/> and re-saves it under
    /// <paramref name="newName"/> — trimmed of surrounding whitespace — removing the old name and
    /// persisting the change. Returns the renamed profile, or <c>null</c> when no profile is stored
    /// under <paramref name="currentName"/>, in which case nothing changes.
    /// </summary>
    /// <param name="currentName">The name of the saved profile to rename.</param>
    /// <param name="newName">
    /// The name to save the profile under instead. Surrounding whitespace is trimmed off; it must not
    /// be empty or whitespace only, since a profile has to be named to be recalled.
    /// </param>
    /// <returns>The renamed profile, or <c>null</c> when no profile is stored under the old name.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="newName"/> is <c>null</c>, empty, or whitespace only; nothing is
    /// changed in that case.
    /// </exception>
    public Profile? Execute(string currentName, string newName)
    {
        var existing = store.Find(currentName);
        if (existing is null)
        {
            return null;
        }

        var renamed = existing.Renamed(newName);
        store.Remove(currentName);
        store.Save(renamed);
        return renamed;
    }
}
