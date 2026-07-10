using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.AddEntry;

/// <summary>
/// Adds a new host entry from an IP address and a hostname, persisting it through the
/// repository so it is written into the <c>hosts</c> file and appears in the list.
/// </summary>
public sealed class AddEntryUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Creates an active entry mapping <paramref name="hostname"/> to
    /// <paramref name="ipAddress"/>, stores it, and returns the created entry.
    /// </summary>
    public HostEntry Execute(string ipAddress, string hostname)
    {
        var entry = new HostEntry(ipAddress, hostname);
        repository.AddEntry(entry);
        return entry;
    }
}
