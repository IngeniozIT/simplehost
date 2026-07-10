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
}
