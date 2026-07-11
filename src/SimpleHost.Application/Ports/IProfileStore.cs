using SimpleHost.Domain.Profiles;

namespace SimpleHost.Application.Ports;

/// <summary>
/// Port giving the application a place to persist named profiles so they can be recalled later.
/// Implemented by infrastructure adapters that know where and how profiles are stored.
/// </summary>
public interface IProfileStore
{
    /// <summary>
    /// Saves <paramref name="profile"/> so it can later be listed and recalled by name. Saving a
    /// profile whose name matches one already stored replaces that earlier profile, since re-saving
    /// under the same name is how the user updates a profile.
    /// </summary>
    /// <param name="profile">The profile to persist.</param>
    void Save(Profile profile);

    /// <summary>
    /// Finds the previously saved profile stored under <paramref name="name"/> so it can be recalled
    /// and activated, matching the name exactly (case-sensitively) as everywhere else in the domain.
    /// Returns <c>null</c> when no profile has been saved under that name.
    /// </summary>
    /// <param name="name">The name of the saved profile to look up.</param>
    /// <returns>The matching profile, or <c>null</c> when none is stored under <paramref name="name"/>.</returns>
    Profile? Find(string name);

    /// <summary>
    /// Returns every previously saved profile so they can all be offered for recall — for example to
    /// populate the list of profiles the user can switch to when the app starts. Returns an empty list
    /// when none has been saved.
    /// </summary>
    /// <returns>All stored profiles.</returns>
    IReadOnlyList<Profile> GetAll();

    /// <summary>
    /// Removes the profile stored under <paramref name="name"/> so it is no longer listed or recalled,
    /// matching the name exactly (case-sensitively) as everywhere else in the domain. A name that
    /// matches no stored profile removes nothing, leaving the store unchanged.
    /// </summary>
    /// <param name="name">The name of the saved profile to remove.</param>
    void Remove(string name);
}
