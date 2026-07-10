using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Infrastructure.Hosts;

/// <summary>
/// Reads an external, standalone <c>hosts</c> file from disk and returns the entries it holds, so
/// they can be imported into the managed configuration. The infrastructure counterpart of
/// <see cref="HostsFileExporter"/>: it reads the file's text and parses it with the same
/// <see cref="HostsFileParser"/> the managed <c>hosts</c> file is read with, so any valid
/// <c>hosts</c> file — whether or not it carries SimpleHost category markers — round-trips into the
/// entries it represents.
/// </summary>
public sealed class HostsFileImporter(HostsFileParser parser) : IHostsFileImporter
{
    /// <summary>
    /// Reads the file at <paramref name="sourcePath"/> and parses every entry it holds — active and
    /// disabled alike — in file order. Category markers, blank lines, and plain comments are
    /// ignored, so the result is a flat list of the file's entries.
    /// </summary>
    /// <param name="sourcePath">The path of the external <c>hosts</c> file to read.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="sourcePath"/> is <c>null</c>, empty, or whitespace only.
    /// </exception>
    public IReadOnlyList<HostEntry> ImportEntries(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        return parser.ParseEntries(File.ReadAllText(sourcePath));
    }
}
