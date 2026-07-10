using SimpleHost.Application.Ports;
using SimpleHost.Domain.Categories;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Reads host entries from a <c>hosts</c> file on disk — active only, or active and disabled.
/// </summary>
public sealed class HostsFileRepository(string filePath, HostsFileParser parser) : IHostsFileRepository
{
    public IReadOnlyList<HostEntry> GetActiveEntries()
        => parser.ParseActiveEntries(File.ReadAllText(filePath));

    public IReadOnlyList<HostEntry> GetEntries()
        => parser.ParseEntries(File.ReadAllText(filePath));

    public IReadOnlyList<CategoryGroup> GetGroupedEntries()
        => parser.ParseGroupedEntries(File.ReadAllText(filePath));
}
