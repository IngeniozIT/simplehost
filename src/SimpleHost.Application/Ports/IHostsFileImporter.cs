using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.Ports;

/// <summary>
/// Port giving the application the ability to read the entries of an external, standalone
/// <c>hosts</c> file chosen by the user, so they can be imported into the managed configuration.
/// The symmetric counterpart of <see cref="IHostsFileExporter"/>: implemented by infrastructure
/// adapters that know the on-disk format, it turns a file on disk back into the entries it holds.
/// </summary>
public interface IHostsFileImporter
{
    /// <summary>
    /// Reads the external <c>hosts</c> file at <paramref name="sourcePath"/> and returns every
    /// entry it holds — active and disabled (commented-out) alike — in file order, each carrying
    /// its <see cref="HostEntry.Status"/> and any trailing inline comment. Category markers and
    /// any other non-entry lines (blanks, plain comments) in the source are ignored, so the result
    /// is a single flat list of entries regardless of how the source file was organised.
    /// </summary>
    /// <param name="sourcePath">The path of the external <c>hosts</c> file to read.</param>
    /// <returns>The entries found in the file, in the order they appear.</returns>
    IReadOnlyList<HostEntry> ImportEntries(string sourcePath);
}
