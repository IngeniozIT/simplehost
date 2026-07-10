using SimpleHost.Application.Ports;
using SimpleHost.Domain.Subscriptions;

namespace SimpleHost.Presentation.Composition;

/// <summary>
/// A placeholder <see cref="ISubscriptionCatalog"/> used so the composition root can build the main
/// view-model before subscription persistence is designed. It reports no kept subscriptions, so a
/// "refresh all" is a no-op; subscripting to and refreshing an individual remote list still works
/// because those paths do not go through the catalog. A later feature replaces this with an adapter
/// that persists the user's subscriptions.
/// </summary>
internal sealed class InMemorySubscriptionCatalog : ISubscriptionCatalog
{
    public IReadOnlyList<Subscription> GetSubscriptions() => [];
}
