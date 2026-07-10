using SimpleHost.Application.Ports;

namespace SimpleHost.Application.UseCases.DeleteCategory;

/// <summary>
/// Deletes a category. The category's <c># [SimpleHost:Category] &lt;Name&gt;</c> marker is removed
/// through the repository while the entries under it are kept, falling back to "No category" so none
/// of them are lost.
/// </summary>
public sealed class DeleteCategoryUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Deletes the category named <paramref name="name"/> — trimmed of surrounding whitespace —
    /// through the repository, keeping its entries under "No category".
    /// </summary>
    /// <param name="name">
    /// The name of the category to delete. Must not be empty or whitespace only, since it has to
    /// identify an existing category.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is <c>null</c>, empty, or whitespace only.
    /// </exception>
    public void Execute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        repository.DeleteCategory(name.Trim());
    }
}
