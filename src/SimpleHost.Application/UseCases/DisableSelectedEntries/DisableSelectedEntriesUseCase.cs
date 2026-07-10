using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Selection;

namespace SimpleHost.Application.UseCases.DisableSelectedEntries;

/// <summary>
/// Disables every entry in a multi-selection in one action, so each selected entry stays in the
/// <c>hosts</c> file but no longer resolves. Each entry's line is rewritten in place as a
/// commented-out (<c>#</c>) line, so it keeps its position — and therefore its order and category
/// membership — while its content (IP address, hostname, inline comment) is preserved and only its
/// status becomes <see cref="HostEntryStatus.Disabled"/>. This is the bulk counterpart of
/// <see cref="DisableEntry.DisableEntryUseCase"/>, applying the same change to a whole selection.
/// </summary>
public sealed class DisableSelectedEntriesUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Replaces each entry in <paramref name="selection"/> with a copy marked
    /// <see cref="HostEntryStatus.Disabled"/>, stores every change, and returns the disabled
    /// entries in the selection's order. An empty selection changes nothing and returns no entries.
    /// </summary>
    /// <param name="selection">The entries the user picked out to disable together.</param>
    public IReadOnlyList<HostEntry> Execute(EntrySelection selection)
    {
        var disabled = new List<HostEntry>(selection.Count);

        foreach (var entry in selection.Entries)
        {
            var disabledEntry = entry with { Status = HostEntryStatus.Disabled };
            repository.UpdateEntry(entry, disabledEntry);
            disabled.Add(disabledEntry);
        }

        return disabled;
    }
}
