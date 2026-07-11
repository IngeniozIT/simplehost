using SimpleHost.Application.Ports;

namespace SimpleHost.Application.UseCases.RemoveProfile;

/// <summary>
/// Removes a previously saved profile by name so it is no longer offered for recall — the delete
/// counterpart of <see cref="SaveProfile.SaveProfileUseCase"/>. The removal is persisted through the
/// store, so it survives a restart. A name that matches no saved profile removes nothing.
/// </summary>
public sealed class RemoveProfileUseCase(IProfileStore store)
{
    /// <summary>
    /// Removes the saved profile stored under <paramref name="name"/> through the store, matching the
    /// name exactly (case-sensitively). A name that matches no saved profile changes nothing.
    /// </summary>
    /// <param name="name">The name of the saved profile to remove.</param>
    public void Execute(string name) => store.Remove(name);
}
