using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.ChangeEntryCategory;

/// <summary>
/// Reassigns an existing host entry to a different category — an existing named category or
/// "No category" — by moving its <c>hosts</c>-file line under the target category's marker block
/// through the repository. The entry's content (IP address, hostname, inline comment, and
/// active/disabled status) is preserved; only its category membership changes.
/// </summary>
public sealed class ChangeEntryCategoryUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Moves <paramref name="entry"/> under <paramref name="targetCategory"/>, persisting the
    /// change through the repository, and returns the entry unchanged.
    /// </summary>
    /// <param name="entry">The existing entry whose category is being changed.</param>
    /// <param name="targetCategory">
    /// The name of the existing category to move the entry under, or <c>null</c> to move it into
    /// "No category".
    /// </param>
    public HostEntry Execute(HostEntry entry, string? targetCategory)
    {
        repository.MoveEntryToCategory(entry, targetCategory);
        return entry;
    }
}
