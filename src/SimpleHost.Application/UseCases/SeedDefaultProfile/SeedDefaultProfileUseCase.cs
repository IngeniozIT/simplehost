using SimpleHost.Application.Ports;
using SimpleHost.Domain.Profiles;

namespace SimpleHost.Application.UseCases.SeedDefaultProfile;

/// <summary>
/// Seeds, on first ever run, a snapshot profile named <c>"Default"</c> that captures the
/// <c>hosts</c> file exactly as it stands, so the user always has a one-click way back to the
/// original file however much they later change it. The snapshot is taken once — the very first time,
/// before anything has been edited — and then persisted, so on every later run the <c>"Default"</c>
/// profile keeps pointing at the genuinely original content rather than being overwritten with a
/// later, already-modified state.
/// </summary>
public sealed class SeedDefaultProfileUseCase(IProfileStore store, IHostsFileContentAccessor content, IHostsFileRepository repository)
{
    /// <summary>The name the seeded snapshot profile is saved under.</summary>
    public const string DefaultProfileName = "Default";

    /// <summary>
    /// Creates and persists the <c>"Default"</c> snapshot profile the first time it is called on a
    /// store that has no such profile yet, capturing the current file content verbatim together with
    /// its grouping. When a <c>"Default"</c> profile already exists — on every run after the first —
    /// nothing is read or written, so the original snapshot is preserved untouched.
    /// </summary>
    public void Execute()
    {
        if (store.Find(DefaultProfileName) is not null)
        {
            return;
        }

        store.Save(Profile.FromSnapshot(
            DefaultProfileName, content.ReadContent(), repository.GetGroupedEntries()));
    }
}
