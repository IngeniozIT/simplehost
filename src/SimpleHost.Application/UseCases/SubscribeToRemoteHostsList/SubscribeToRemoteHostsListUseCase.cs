using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.SubscribeToRemoteHostsList;

/// <summary>
/// Subscribes to a remote <c>hosts</c> list (for example a public ad-block list) served at a URL,
/// placing its entries into a dedicated category of their own so they stay separate from the
/// entries the user manages by hand. The remote list is fetched through the fetcher — every entry
/// it holds, active and disabled alike, in list order — a new category named for the subscription
/// is created, and each fetched entry is added under that category through the repository, so the
/// subscribed mappings become part of the current configuration and are returned by subsequent
/// reads as their own group.
/// </summary>
/// <remarks>
/// The subscription owns its category exclusively: a category with the chosen name must not already
/// exist, since mixing fetched entries into a manually managed category would blur the separation
/// the feature exists to keep. The name is matched with an exact, case-sensitive comparison and is
/// rejected before the remote list is fetched. The list is fetched before the category is created,
/// so a failed fetch leaves no dangling empty category behind. Whatever categories the remote list
/// itself declares are ignored: all of its entries are collapsed into the single dedicated category.
/// </remarks>
public sealed class SubscribeToRemoteHostsListUseCase(
    IRemoteHostsListFetcher fetcher,
    IHostsFileRepository repository)
{
    /// <summary>
    /// Subscribes to the remote <c>hosts</c> list at <paramref name="url"/>, placing its entries
    /// into a new dedicated category named <paramref name="category"/> — trimmed of surrounding
    /// whitespace — and returns the fetched entries, in the order they were read.
    /// </summary>
    /// <param name="url">
    /// The URL of the remote <c>hosts</c> list to subscribe to. Must not be <c>null</c>, empty, or
    /// whitespace only.
    /// </param>
    /// <param name="category">
    /// The name of the dedicated category to place the subscription's entries into. Surrounding
    /// whitespace is trimmed off; the name must not be empty or whitespace only, and must not name a
    /// category that already exists.
    /// </param>
    /// <returns>The entries that were fetched, in the order they appear in the remote list.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="url"/> or <paramref name="category"/> is <c>null</c>, empty, or
    /// whitespace only.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="category"/> names a category that already exists.
    /// </exception>
    public IReadOnlyList<HostEntry> Execute(string url, string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        var name = category.Trim();
        if (repository.GetGroupedEntries().Any(group => group.Name == name))
        {
            throw new InvalidOperationException($"Category '{name}' already exists.");
        }

        var entries = fetcher.Fetch(url);

        repository.CreateCategory(name);
        foreach (var entry in entries)
        {
            repository.AddEntry(entry, name);
        }

        return entries;
    }
}
