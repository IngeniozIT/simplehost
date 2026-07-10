using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.MoveEntryBetweenCategories;

/// <summary>
/// Moves an existing host entry from one category into another at a chosen drop position — the
/// drag-and-drop move of an entry between categories — persisting the change through the
/// repository. The entry's <c>hosts</c>-file line is relocated into the target category's marker
/// block at the drop position, so both its new category membership and its order are persisted
/// purely by line order. The entry's content (IP address, hostname, inline comment, and
/// active/disabled status) is preserved; only its category membership and position change.
/// </summary>
public sealed class MoveEntryBetweenCategoriesUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Moves <paramref name="entry"/> into <paramref name="targetCategory"/> at the zero-based
    /// drop position <paramref name="targetIndex"/>, persisting the change through the repository,
    /// and returns the entry unchanged.
    /// </summary>
    /// <param name="entry">The existing entry being dragged to another category.</param>
    /// <param name="targetCategory">
    /// The name of the existing category to move the entry under, or <c>null</c> to move it into
    /// "No category".
    /// </param>
    /// <param name="targetIndex">
    /// The zero-based drop position the entry should occupy among the target category's entries
    /// after the move.
    /// </param>
    public HostEntry Execute(HostEntry entry, string? targetCategory, int targetIndex)
    {
        repository.MoveEntryToCategory(entry, targetCategory, targetIndex);
        return entry;
    }
}
