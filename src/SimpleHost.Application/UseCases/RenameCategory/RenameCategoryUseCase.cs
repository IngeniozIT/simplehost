using SimpleHost.Application.Ports;

namespace SimpleHost.Application.UseCases.RenameCategory;

/// <summary>
/// Renames an existing category. The category's <c># [SimpleHost:Category] &lt;Name&gt;</c> marker
/// is rewritten with the new name through the repository, while the entries under it remain in
/// place, so the group keeps its entries and only its name changes.
/// </summary>
public sealed class RenameCategoryUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Renames the category currently named <paramref name="currentName"/> to
    /// <paramref name="newName"/> — trimmed of surrounding whitespace — through the repository, and
    /// returns the trimmed new name that was stored.
    /// </summary>
    /// <param name="currentName">
    /// The current name of the category to rename. Must not be empty or whitespace only, since it
    /// has to identify an existing category.
    /// </param>
    /// <param name="newName">
    /// The new name for the category. Surrounding whitespace is trimmed off; the name must not be
    /// empty or whitespace only, since a category must be named to appear as its own group.
    /// </param>
    /// <returns>The trimmed new category name that was stored.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="currentName"/> or <paramref name="newName"/> is <c>null</c>,
    /// empty, or whitespace only.
    /// </exception>
    public string Execute(string currentName, string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        var trimmed = newName.Trim();
        repository.RenameCategory(currentName, trimmed);
        return trimmed;
    }
}
