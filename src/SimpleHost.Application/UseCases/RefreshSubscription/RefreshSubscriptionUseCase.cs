using SimpleHost.Application.Ports;
using SimpleHost.Domain.Entries;

namespace SimpleHost.Application.UseCases.RefreshSubscription;

/// <summary>
/// Refreshes an existing remote subscription on demand: re-fetches the remote <c>hosts</c> list the
/// subscription was created from and rewrites its dedicated category so the category's entries match
/// the latest fetched content exactly. The subscription is identified by the URL to re-fetch and the
/// name of the category that holds it — the same pair used to create it through
/// <see cref="SubscribeToRemoteHostsList.SubscribeToRemoteHostsListUseCase"/>.
/// </summary>
/// <remarks>
/// The category must already exist, since a subscription can only be refreshed once it has been
/// created; the name is matched with an exact, case-sensitive comparison and a missing category is
/// rejected before the remote list is fetched. The list is fetched before the category is touched, so
/// a failed fetch leaves the existing entries in place rather than emptying the category. On a
/// successful fetch the category's current entries are removed and the freshly fetched entries — every
/// one the list holds, active and disabled alike, in list order — are added under it, so the category
/// ends up holding exactly the latest content. The category's marker stays in place, keeping its
/// position among the other categories.
/// </remarks>
public sealed class RefreshSubscriptionUseCase(
    IRemoteHostsListFetcher fetcher,
    IHostsFileRepository repository)
{
    /// <summary>
    /// Refreshes the subscription held in the category named <paramref name="category"/> — trimmed of
    /// surrounding whitespace — by re-fetching the remote <c>hosts</c> list at <paramref name="url"/>
    /// and replacing the category's entries with the freshly fetched ones, and returns those entries
    /// in the order they were read.
    /// </summary>
    /// <param name="url">
    /// The URL of the remote <c>hosts</c> list to re-fetch. Must not be <c>null</c>, empty, or
    /// whitespace only.
    /// </param>
    /// <param name="category">
    /// The name of the dedicated category holding the subscription. Surrounding whitespace is trimmed
    /// off; the name must not be empty or whitespace only, and must name a category that already
    /// exists.
    /// </param>
    /// <returns>The entries that were fetched, in the order they appear in the remote list.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="url"/> or <paramref name="category"/> is <c>null</c>, empty, or
    /// whitespace only.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="category"/> does not name a category that already exists.
    /// </exception>
    public IReadOnlyList<HostEntry> Execute(string url, string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        var name = category.Trim();
        var group = repository.GetGroupedEntries().FirstOrDefault(group => group.Name == name)
            ?? throw new InvalidOperationException($"Category '{name}' does not exist.");

        var entries = fetcher.Fetch(url);

        foreach (var existing in group.Entries)
        {
            repository.DeleteEntry(existing);
        }

        foreach (var entry in entries)
        {
            repository.AddEntry(entry, name);
        }

        return entries;
    }
}
