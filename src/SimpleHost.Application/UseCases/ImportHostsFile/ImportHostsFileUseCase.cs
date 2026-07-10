using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.ImportHostsFile;

/// <summary>
/// Imports the entries of an external, user-picked <c>hosts</c> file into a chosen category of the
/// managed configuration. The external file is read through the importer — every entry it holds,
/// active and disabled alike, in file order — and each entry is added under the chosen category
/// through the repository, so the imported mappings become part of the current configuration and
/// are returned by subsequent reads.
/// </summary>
/// <remarks>
/// The category is chosen from those that already exist, so matching is an exact, case-sensitive
/// comparison against the category name. A <c>null</c> category imports the entries into the
/// special "No category" group. A named category that does not exist cannot be the import target
/// and is rejected before the external file is read. Whatever categories the source file itself
/// declares are ignored: all of its entries are collapsed into the single chosen category.
/// </remarks>
public sealed class ImportHostsFileUseCase(IHostsFileImporter importer, IHostsFileRepository repository)
{
    /// <summary>
    /// Imports the entries of the external <c>hosts</c> file at <paramref name="sourcePath"/> into
    /// the category named <paramref name="category"/> — or the "No category" group when it is
    /// <c>null</c> — and returns the imported entries, in the order they were read.
    /// </summary>
    /// <param name="sourcePath">
    /// The path of the external <c>hosts</c> file to import. Must not be <c>null</c>, empty, or
    /// whitespace only.
    /// </param>
    /// <param name="category">
    /// The name of an existing category to import the entries into, or <c>null</c> to import them
    /// into the "No category" group.
    /// </param>
    /// <returns>The entries that were imported, in the order they appear in the source file.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="sourcePath"/> is <c>null</c>, empty, or whitespace only.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="category"/> names a category that does not exist.
    /// </exception>
    public IReadOnlyList<HostEntry> Execute(string sourcePath, string? category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (category is not null && repository.GetGroupedEntries().All(group => group.Name != category))
        {
            throw new InvalidOperationException($"Category '{category}' does not exist.");
        }

        var entries = importer.ImportEntries(sourcePath);

        foreach (var entry in entries)
        {
            repository.AddEntry(entry, category);
        }

        return entries;
    }
}
