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
}
