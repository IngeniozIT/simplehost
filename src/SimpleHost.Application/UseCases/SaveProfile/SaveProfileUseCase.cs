using SimpleHost.Application.Ports;
using SimpleHost.Domain.Profiles;

namespace SimpleHost.Application.UseCases.SaveProfile;

/// <summary>
/// Saves a named profile capturing which categories should be enabled — for example <c>"Work"</c>
/// or <c>"Home"</c> — so the choice can be recalled later. The profile records only the enabled
/// categories; recalling it (a separate action) enables those and disables every other category.
/// </summary>
public sealed class SaveProfileUseCase(IProfileStore store)
{
    /// <summary>
    /// Builds a <see cref="Profile"/> named <paramref name="name"/> — trimmed of surrounding
    /// whitespace — that enables <paramref name="enabledCategories"/> (deduplicated, with
    /// <c>null</c> meaning the "No category" group), persists it through the store, and returns the
    /// stored profile. A profile saved under a name that already exists replaces the previous one.
    /// </summary>
    /// <param name="name">
    /// The name to save the profile under. Surrounding whitespace is trimmed off; the name must not
    /// be empty or whitespace only, since a profile has to be named to be recalled.
    /// </param>
    /// <param name="enabledCategories">
    /// The names of the categories the profile should enable, or <c>null</c> among them for the
    /// "No category" group. May be empty for a profile that disables everything.
    /// </param>
    /// <returns>The profile that was stored, with its name trimmed and categories deduplicated.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is <c>null</c>, empty, or whitespace only; nothing is
    /// saved in that case.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="enabledCategories"/> is <c>null</c>; nothing is saved in that case.
    /// </exception>
    public Profile Execute(string name, IEnumerable<string?> enabledCategories)
    {
        var profile = Profile.Of(name, enabledCategories);
        store.Save(profile);
        return profile;
    }
}
