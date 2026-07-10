namespace SimpleHost.Domain.Subscriptions;

/// <summary>
/// A remote <c>hosts</c> list the user has subscribed to: the URL the list is fetched from and the
/// name of the dedicated category that holds its entries. This is the identifying pair a
/// subscription is created and refreshed by — the same <c>(url, category)</c> used to subscribe
/// through the subscribe use case and to refresh a single subscription on demand — kept so the
/// subscription can be re-fetched automatically to stay up to date.
/// </summary>
/// <remarks>
/// A subscription owns its category exclusively, so the category name identifies it within the
/// configuration; the URL says where its content comes from. Both are matched exactly
/// (case-sensitively) as everywhere else in the domain. The pair is a persisted concept, recalled
/// through the subscription catalog port much as a <see cref="Profiles.Profile"/> is recalled
/// through its store.
/// </remarks>
public sealed record Subscription
{
    private Subscription(string url, string category)
    {
        Url = url;
        Category = category;
    }

    /// <summary>
    /// The URL of the remote <c>hosts</c> list to fetch. Trimmed of surrounding whitespace; never
    /// empty or whitespace only.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// The name of the dedicated category holding the subscription's entries. Trimmed of surrounding
    /// whitespace; never empty or whitespace only.
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// Creates a subscription for the remote list at <paramref name="url"/> held in the category
    /// named <paramref name="category"/>, each trimmed of surrounding whitespace.
    /// </summary>
    /// <param name="url">
    /// The URL of the remote <c>hosts</c> list. Surrounding whitespace is trimmed off; must not be
    /// <c>null</c>, empty, or whitespace only.
    /// </param>
    /// <param name="category">
    /// The name of the dedicated category holding the subscription. Surrounding whitespace is trimmed
    /// off; must not be <c>null</c>, empty, or whitespace only.
    /// </param>
    /// <returns>The subscription, with its URL and category trimmed.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="url"/> or <paramref name="category"/> is <c>null</c>, empty, or
    /// whitespace only.
    /// </exception>
    public static Subscription Of(string url, string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        return new Subscription(url.Trim(), category.Trim());
    }
}
