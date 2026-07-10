using SimpleHost.Application.Ports;

namespace SimpleHost.Application.UseCases.CreateCategory;

/// <summary>
/// Creates a new, named category to organise entries under. The category is persisted through the
/// repository as a <c># [SimpleHost:Category] &lt;Name&gt;</c> marker in the <c>hosts</c> file and
/// starts out empty, appearing as an empty group in the list until entries are placed under it.
/// </summary>
public sealed class CreateCategoryUseCase(IHostsFileRepository repository)
{
    /// <summary>
    /// Creates a category named <paramref name="name"/> — trimmed of surrounding whitespace —
    /// through the repository, and returns the trimmed name that was stored.
    /// </summary>
    /// <param name="name">
    /// The name of the category to create. Surrounding whitespace is trimmed off; the name must not
    /// be empty or whitespace only, since a category must be named to appear as its own group.
    /// </param>
    /// <returns>The trimmed category name that was created.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is <c>null</c>, empty, or whitespace only.
    /// </exception>
    public string Execute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();
        repository.CreateCategory(trimmed);
        return trimmed;
    }
}
