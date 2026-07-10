using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Selection;

namespace SimpleHost.Application.UseCases.MoveSelectedEntriesToCategory;

/// <summary>
/// Moves every entry in a multi-selection into a chosen target category in one action, relocating
/// each selected entry's <c>hosts</c>-file line under that category's
/// <c># [SimpleHost:Category] &lt;Name&gt;</c> marker block — appended as the block's last entry — or,
/// when the target is <c>null</c>, into the "No category" region. Each entry's content (IP address,
/// hostname, inline comment, and active/disabled status) is preserved; only its category membership
/// changes. This is the bulk counterpart of
/// <see cref="ChangeEntryCategory.ChangeEntryCategoryUseCase"/>, applying the same reassignment to a
/// whole selection so a group of entries can be re-categorized together.
/// </summary>
public sealed class MoveSelectedEntriesToCategoryUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Moves each entry in <paramref name="selection"/> under <paramref name="targetCategory"/>,
    /// persisting every change through the repository, and returns the moved entries in the
    /// selection's order. An empty selection changes nothing and returns no entries.
    /// </summary>
    /// <param name="selection">The entries the user picked out to move together.</param>
    /// <param name="targetCategory">
    /// The name of the existing category to move the selected entries under, or <c>null</c> to move
    /// them into "No category".
    /// </param>
    public IReadOnlyList<HostEntry> Execute(EntrySelection selection, string? targetCategory)
    {
        var moved = new List<HostEntry>(selection.Count);

        foreach (var entry in selection.Entries)
        {
            repository.MoveEntryToCategory(entry, targetCategory);
            moved.Add(entry);
        }

        return moved;
    }
}
