using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Selection;

namespace SimpleHost.Application.UseCases.EnableSelectedEntries;

/// <summary>
/// Enables every entry in a multi-selection in one action, so each selected disabled entry
/// resolves again. Each entry's line is rewritten in place with the leading <c>#</c> that was
/// disabling it removed, so it keeps its position — and therefore its order and category
/// membership — while its content (IP address, hostname, inline comment) is preserved and only its
/// status becomes <see cref="HostEntryStatus.Active"/>. This is the bulk counterpart of
/// <see cref="EnableEntry.EnableEntryUseCase"/>, applying the same change to a whole selection.
/// </summary>
public sealed class EnableSelectedEntriesUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Replaces each entry in <paramref name="selection"/> with a copy marked
    /// <see cref="HostEntryStatus.Active"/>, stores every change, and returns the enabled entries
    /// in the selection's order. An empty selection changes nothing and returns no entries.
    /// </summary>
    /// <param name="selection">The entries the user picked out to enable together.</param>
    public IReadOnlyList<HostEntry> Execute(EntrySelection selection)
    {
        var enabled = new List<HostEntry>(selection.Count);

        foreach (var entry in selection.Entries)
        {
            var enabledEntry = entry with { Status = HostEntryStatus.Active };
            repository.UpdateEntry(entry, enabledEntry);
            enabled.Add(enabledEntry);
        }

        return enabled;
    }
}
