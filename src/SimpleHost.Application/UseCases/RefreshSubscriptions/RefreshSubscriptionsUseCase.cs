using SimpleHost.Application.Ports;
using SimpleHost.Application.UseCases.RefreshSubscription;

namespace SimpleHost.Application.UseCases.RefreshSubscriptions;

/// <summary>
/// Refreshes every kept subscription automatically, so remote lists stay up to date without the
/// user asking. This is the unattended, batch counterpart of
/// <see cref="RefreshSubscription.RefreshSubscriptionUseCase"/>: it reads the current subscriptions
/// from the catalog and, for each, re-fetches its remote <c>hosts</c> list and rewrites its
/// dedicated category so the category holds exactly the latest fetched content. It is the action a
/// scheduler runs on each tick; the periodic trigger itself lives in the app's outer shell, not in
/// this use case.
/// </summary>
/// <remarks>
/// Because the refresh runs unattended, one subscription's problem must not stop the others: each
/// subscription is refreshed independently and any failure — the remote list being unreachable, or
/// its dedicated category having since been removed — is captured as a failed result for that
/// subscription while the remaining subscriptions still refresh. Each single refresh fetches before
/// it touches the category, so a failed fetch leaves that subscription's existing entries in place
/// rather than emptying its category. The outcome of every subscription is returned in catalog
/// order so the app can report what was updated and what could not be.
/// </remarks>
public sealed class RefreshSubscriptionsUseCase(
    ISubscriptionCatalog catalog,
    IRemoteHostsListFetcher fetcher,
    IHostsFileRepository repository)
{
    private readonly RefreshSubscriptionUseCase _refreshOne = new(fetcher, repository);

    /// <summary>
    /// Refreshes every subscription the catalog currently holds, rewriting each one's dedicated
    /// category with the entries freshly fetched from its remote list, and returns the outcome of
    /// each — refreshed with its fetched entries, or failed with the reason — in catalog order. A
    /// subscription whose refresh fails leaves its category untouched and does not stop the others.
    /// Returns an empty list when nothing is subscribed, without fetching or changing anything.
    /// </summary>
    /// <returns>The outcome of refreshing each subscription, in catalog order.</returns>
    public IReadOnlyList<SubscriptionRefreshResult> Execute()
    {
        var results = new List<SubscriptionRefreshResult>();

        foreach (var subscription in catalog.GetSubscriptions())
        {
            try
            {
                var entries = _refreshOne.Execute(subscription.Url, subscription.Category);
                results.Add(SubscriptionRefreshResult.Refreshed(subscription, entries));
            }
            catch (Exception exception)
            {
                results.Add(SubscriptionRefreshResult.Failed(subscription, exception.Message));
            }
        }

        return results;
    }
}
