using SimpleHost.Application.Ports;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Profiles;

namespace SimpleHost.Application.UseCases.ActivateProfile;

/// <summary>
/// Activates a previously saved profile in one action, switching the whole <c>hosts</c> file to the
/// preset it records: every category the profile lists is enabled and every other category is
/// disabled. This is the recall counterpart of
/// <see cref="SaveProfile.SaveProfileUseCase"/> — the profile stores only the enabled set, so
/// activating it enables those categories and disables all the rest, applying the category-wide
/// enable/disable of <see cref="EnableCategory.EnableCategoryUseCase"/> and
/// <see cref="DisableCategory.DisableCategoryUseCase"/> across every category at once.
/// </summary>
public sealed class ActivateProfileUseCase(IProfileStore store, IHostsFileRepository repository)
{
    /// <summary>
    /// Looks up the saved profile named <paramref name="name"/> and applies it: each entry of a
    /// category the profile lists is rewritten <see cref="HostEntryStatus.Active"/>, and each entry
    /// of every other category — including the "No category" group when the profile does not list it
    /// — is rewritten <see cref="HostEntryStatus.Disabled"/>. Every entry's line is replaced in
    /// place, so its position, order, category membership, and content (IP, hostname, inline comment)
    /// are preserved and only its status changes. Categories are matched by exact name
    /// (case-sensitively), with <c>null</c> standing for the "No category" group; a category the
    /// profile lists that holds no entries — or does not exist — simply changes nothing. Returns the
    /// entries grouped by category, in file order, each carrying its new status.
    /// </summary>
    /// <param name="name">The name of the saved profile to activate.</param>
    /// <returns>The entries grouped by category after activation, in file order.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no profile has been saved under <paramref name="name"/>; nothing is changed in
    /// that case.
    /// </exception>
    public IReadOnlyList<CategoryGroup> Execute(string name)
    {
        var profile = store.Find(name)
            ?? throw new InvalidOperationException($"No saved profile named '{name}'.");

        var result = new List<CategoryGroup>();

        foreach (var group in repository.GetGroupedEntries())
        {
            var targetStatus = IsEnabledBy(profile, group.Name)
                ? HostEntryStatus.Active
                : HostEntryStatus.Disabled;

            var switched = new List<HostEntry>(group.Entries.Count);
            foreach (var entry in group.Entries)
            {
                var replacement = entry with { Status = targetStatus };
                repository.UpdateEntry(entry, replacement);
                switched.Add(replacement);
            }

            result.Add(group with { Entries = switched });
        }

        return result;
    }

    private static bool IsEnabledBy(Profile profile, string? category)
        => profile.EnabledCategories.Any(enabled => string.Equals(enabled, category, StringComparison.Ordinal));
}
