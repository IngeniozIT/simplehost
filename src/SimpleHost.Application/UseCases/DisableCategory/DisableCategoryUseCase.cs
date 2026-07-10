using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.DisableCategory;

/// <summary>
/// Disables every entry within a single chosen category in one action, so the whole group stays in
/// the <c>hosts</c> file but no longer resolves. Each entry's line is rewritten in place as a
/// commented-out (<c>#</c>) line, so it keeps its position — and therefore its order and category
/// membership — while its content (IP address, hostname, inline comment) is preserved and only its
/// status becomes <see cref="HostEntryStatus.Disabled"/>. This is the category-wide counterpart of
/// <see cref="DisableSelectedEntries.DisableSelectedEntriesUseCase"/>, applying the same change to a
/// whole category rather than a hand-picked selection.
/// </summary>
public sealed class DisableCategoryUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Replaces each entry of the chosen category with a copy marked
    /// <see cref="HostEntryStatus.Disabled"/>, stores every change, and returns the disabled entries
    /// in file order. The category is matched by exact name against the existing groups; a
    /// <c>null</c> <paramref name="category"/> selects the special "No category" group of entries
    /// that appear before any marker. A category that holds no entries, or one that does not exist,
    /// changes nothing and returns no entries.
    /// </summary>
    /// <param name="category">
    /// The name of the category to disable, or <c>null</c> for the "No category" group.
    /// </param>
    public IReadOnlyList<HostEntry> Execute(string? category)
    {
        var entries = repository.GetGroupedEntries()
            .Where(group => group.Name == category)
            .SelectMany(group => group.Entries)
            .ToList();

        var disabled = new List<HostEntry>(entries.Count);

        foreach (var entry in entries)
        {
            var disabledEntry = entry with { Status = HostEntryStatus.Disabled };
            repository.UpdateEntry(entry, disabledEntry);
            disabled.Add(disabledEntry);
        }

        return disabled;
    }
}
