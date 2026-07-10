using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Reads the active entries from a <c>hosts</c> file on disk.
/// </summary>
public sealed class HostsFileRepository(string filePath, HostsFileParser parser) : IHostsFileRepository
{
    public IReadOnlyList<HostEntry> GetActiveEntries()
        => parser.ParseActiveEntries(File.ReadAllText(filePath));
}
