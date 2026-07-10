using SimpleHost.Domain.Categories;

namespace SimpleHost.Application.Ports;

/// <summary>
/// Port giving the application the ability to export a category as a standalone <c>hosts</c>
/// file on disk. Implemented by infrastructure adapters that know the on-disk format, so the
/// written file is a valid, portable <c>hosts</c> file — carrying the category's
/// <c># [SimpleHost:Category] &lt;Name&gt;</c> marker followed by its entries — that can be used
/// elsewhere.
/// </summary>
public interface IHostsFileExporter
{
    /// <summary>
    /// Writes <paramref name="group"/> to <paramref name="destinationPath"/> as a standalone
    /// <c>hosts</c> file: the category's <c># [SimpleHost:Category] &lt;Name&gt;</c> marker line
    /// (omitted for the "No category" group, whose <see cref="CategoryGroup.Name"/> is
    /// <c>null</c>) followed by each of the group's entries — active and disabled alike — rendered
    /// as their <c>hosts</c>-file lines in the order they appear. Any existing file at the
    /// destination is overwritten.
    /// </summary>
    /// <param name="group">The category, and its entries, to export.</param>
    /// <param name="destinationPath">The path of the <c>hosts</c> file to write.</param>
    void ExportCategory(CategoryGroup group, string destinationPath);

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
