using SimpleHost.Domain.Entries;
using SimpleHost.Domain.Subscriptions;

namespace SimpleHost.Application.UseCases.RefreshSubscriptions;

/// <summary>
/// The outcome of trying to refresh one subscription during an automatic refresh of them all:
/// either the subscription's category was rewritten with the freshly fetched entries, or the
/// refresh failed and its category was left untouched. Reported per subscription so the app can
/// surface what happened without one failure hiding the rest.
/// </summary>
/// <param name="Subscription">The subscription this result is for.</param>
/// <param name="Succeeded">
/// <c>true</c> when the subscription's category was refreshed with the fetched entries;
/// <c>false</c> when the refresh failed and the category was left as it was.
/// </param>
/// <param name="Entries">
/// The freshly fetched entries now held in the category when <see cref="Succeeded"/> is
/// <c>true</c>, in the order they were read; empty when the refresh failed.
/// </param>
/// <param name="Error">
/// The reason the refresh failed when <see cref="Succeeded"/> is <c>false</c>; <c>null</c> when it
/// succeeded.
/// </param>
public sealed record SubscriptionRefreshResult(
    Subscription Subscription,
    bool Succeeded,
    IReadOnlyList<HostEntry> Entries,
    string? Error)
{
    /// <summary>
    /// Builds the result of a subscription whose category was successfully refreshed with the
    /// freshly fetched <paramref name="entries"/>.
    /// </summary>
    /// <param name="subscription">The subscription that was refreshed.</param>
    /// <param name="entries">The entries fetched and now held in its category, in list order.</param>
    /// <returns>A succeeded result carrying the fetched entries and no error.</returns>
    public static SubscriptionRefreshResult Refreshed(
        Subscription subscription,
        IReadOnlyList<HostEntry> entries)
        => new(subscription, true, entries, null);

    /// <summary>
    /// Builds the result of a subscription whose refresh failed, leaving its category untouched.
    /// </summary>
    /// <param name="subscription">The subscription that failed to refresh.</param>
    /// <param name="error">The reason the refresh failed.</param>
    /// <returns>A failed result carrying the reason and no entries.</returns>
    public static SubscriptionRefreshResult Failed(Subscription subscription, string error)
        => new(subscription, false, Array.Empty<HostEntry>(), error);
}
