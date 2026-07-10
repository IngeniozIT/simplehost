using SimpleHost.Application.Ports;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Reads host entries from a <c>hosts</c> file on disk — active only, or active and disabled —
/// and adds new entries by appending them to the file.
/// </summary>
public sealed class HostsFileRepository(
    string filePath,
    HostsFileParser parser,
    HostsFileSerializer serializer) : IHostsFileRepository
{
    public void AddEntry(HostEntry entry)
    {
        var line = serializer.SerializeEntry(entry);
        var existing = File.ReadAllText(filePath);

        var separator = existing.Length > 0 && !existing.EndsWith('\n') ? "\n" : string.Empty;
        File.WriteAllText(filePath, $"{existing}{separator}{line}\n");
    }

    public IReadOnlyList<HostEntry> GetActiveEntries()
        => parser.ParseActiveEntries(File.ReadAllText(filePath));

    public IReadOnlyList<HostEntry> GetEntries()
        => parser.ParseEntries(File.ReadAllText(filePath));

    public IReadOnlyList<CategoryGroup> GetGroupedEntries()
        => parser.ParseGroupedEntries(File.ReadAllText(filePath));
}
