using SimpleHost.Application.Ports;

namespace SimpleHost.Application.UseCases.ExportCategory;

/// <summary>
/// Exports a single chosen category as a standalone <c>hosts</c> file on disk, so its entries can
/// be used elsewhere. The category is read through the repository — active and disabled entries
/// alike, in file order — and handed to the exporter, which writes it out as a valid
/// <c>hosts</c> file carrying the category's <c># [SimpleHost:Category] &lt;Name&gt;</c> marker.
/// </summary>
/// <remarks>
/// The category is chosen from those that already exist, so matching is an exact, case-sensitive
/// comparison against the category name. A <c>null</c> category selects the special "No category"
/// group of entries that appear before any marker (exported without a marker line). An empty
/// category is exported as just its marker. A named category that does not exist cannot be
/// exported and is rejected.
/// </remarks>
public sealed class ExportCategoryUseCase(IHostsFileRepository repository, IHostsFileExporter exporter)
{
    /// <summary>
    /// Exports the category named <paramref name="category"/> — or the "No category" group when it
    /// is <c>null</c> — to <paramref name="destinationPath"/>.
    /// </summary>
    /// <param name="category">
    /// The name of an existing category to export, or <c>null</c> to export the "No category" group.
    /// </param>
    /// <param name="destinationPath">
    /// The path of the standalone <c>hosts</c> file to write. Must not be empty or whitespace only.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="destinationPath"/> is <c>null</c>, empty, or whitespace only.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="category"/> names a category that does not exist.
    /// </exception>
    public void Execute(string? category, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var group = repository.GetGroupedEntries().FirstOrDefault(group => group.Name == category)
            ?? throw new InvalidOperationException($"Category '{category}' does not exist.");

        exporter.ExportCategory(group, destinationPath);
    }
}
