using SimpleHost.Domain.Categories;

namespace SimpleHost.Application.Ports;

/// <summary>
/// Port giving the application the ability to export the whole configuration as a standalone
/// <c>hosts</c> file on disk. Implemented by infrastructure adapters that know the on-disk format,
/// so the written file is a valid, portable <c>hosts</c> file — carrying each category's
/// <c># [SimpleHost:Category] &lt;Name&gt;</c> marker followed by its entries — that can be used
/// elsewhere.
/// </summary>
public interface IHostsFileExporter
{
    /// <summary>
    /// Writes the whole configuration — <paramref name="groups"/>, in the order given — to
    /// <paramref name="destinationPath"/> as a single valid, standalone <c>hosts</c> file. The
    /// "No category" group (whose <see cref="CategoryGroup.Name"/> is <c>null</c>) contributes its
    /// entries with no marker line; every other group contributes its
    /// <c># [SimpleHost:Category] &lt;Name&gt;</c> marker followed by its entries — active and
    /// disabled alike, in order — so the result mirrors the managed <c>hosts</c> file's own layout
    /// and round-trips back through the reader to the same groups. An empty group contributes just
    /// its marker (and the empty "No category" group contributes nothing). Any existing file at the
    /// destination is overwritten.
    /// </summary>
    /// <param name="groups">Every category group, in file order, to write to the file.</param>
    /// <param name="destinationPath">The path of the <c>hosts</c> file to write.</param>
    void ExportConfiguration(IReadOnlyList<CategoryGroup> groups, string destinationPath);
}
