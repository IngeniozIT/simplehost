using SimpleHost.Domain.Subscriptions;

namespace SimpleHost.Application.Ports;

/// <summary>
/// Port giving the application the set of remote-list subscriptions the user currently keeps, so
/// they can be refreshed automatically to stay up to date. Implemented by an infrastructure adapter
/// that knows where and how subscriptions are persisted — the network-list counterpart of
/// <see cref="IProfileStore"/>, which recalls saved profiles.
/// </summary>
public interface ISubscriptionCatalog
{
    /// <summary>
    /// Returns every subscription currently kept — each carrying the URL to re-fetch and the name of
    /// the dedicated category that holds it — so an automatic refresh can bring each one's category
    /// up to date. Returns an empty list when nothing is subscribed.
    /// </summary>
    /// <returns>The known subscriptions, in the order they should be refreshed.</returns>
    IReadOnlyList<Subscription> GetSubscriptions();
}
