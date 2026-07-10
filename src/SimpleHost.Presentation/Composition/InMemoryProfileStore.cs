using SimpleHost.Application.Ports;
using SimpleHost.Domain.Profiles;

namespace SimpleHost.Presentation.Composition;

/// <summary>
/// A process-lifetime, in-memory <see cref="IProfileStore"/> used so the composition root can build
/// the main view-model before profile persistence is designed. Profiles saved during a run can be
/// listed and recalled for the rest of that run, but are not written anywhere and do not survive a
/// restart. A later feature replaces this with an adapter that persists profiles.
/// </summary>
/// <remarks>
/// Matching follows the domain's rule: profile names are compared exactly (case-sensitively), and
/// re-saving under an existing name replaces the earlier profile.
/// </remarks>
internal sealed class InMemoryProfileStore : IProfileStore
{
    private readonly Dictionary<string, Profile> _profilesByName = new(StringComparer.Ordinal);

    public void Save(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        _profilesByName[profile.Name] = profile;
    }

    public Profile? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _profilesByName.TryGetValue(name, out var profile) ? profile : null;
    }
}
