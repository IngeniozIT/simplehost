namespace SimpleHost.Domain.Profiles;

/// <summary>
/// A named, saved preset that captures which categories should be enabled — for example
/// <c>"Work"</c> or <c>"Home"</c> — so the choice can be recalled later. Recalling a profile
/// enables the categories it lists and disables every other category, so the profile records only
/// the enabled set; anything not in the set is, by definition, meant to be disabled.
/// </summary>
/// <remarks>
/// The enabled categories are identified by name, matched exactly (case-sensitively) as everywhere
/// else in the domain, with <c>null</c> standing for the special "No category" group. The set is
/// deduplicated on construction — listing the same category twice is a no-op — and kept in the order
/// the categories were first given. An empty set is allowed: it is a profile that disables
/// everything. Names of categories are stored as given, since a profile references categories that
/// already exist rather than creating them.
/// </remarks>
public sealed record Profile
{
    private Profile(string name, IReadOnlyList<string?> enabledCategories)
    {
        Name = name;
        EnabledCategories = enabledCategories;
    }

    /// <summary>The profile's name, as it is shown and recalled by. Never empty or whitespace.</summary>
    public string Name { get; }

    /// <summary>
    /// The categories to enable when this profile is recalled, deduplicated and in the order they
    /// were first given. <c>null</c> stands for the "No category" group. Every category not in this
    /// set is meant to be disabled. May be empty.
    /// </summary>
    public IReadOnlyList<string?> EnabledCategories { get; }

    /// <summary>
    /// Creates a profile named <paramref name="name"/> — trimmed of surrounding whitespace — that
    /// enables the given <paramref name="enabledCategories"/>. Duplicate categories (matched exactly,
    /// case-sensitively, with <c>null</c> meaning "No category") are collapsed to a single entry,
    /// keeping the order of first appearance.
    /// </summary>
    /// <param name="name">
    /// The name to save the profile under. Surrounding whitespace is trimmed off; the name must not
    /// be empty or whitespace only, since a profile has to be named to be recalled.
    /// </param>
    /// <param name="enabledCategories">
    /// The names of the categories the profile should enable, or <c>null</c> among them for the
    /// "No category" group. May be empty for a profile that disables everything.
    /// </param>
    /// <returns>The profile, with its name trimmed and its enabled categories deduplicated.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is <c>null</c>, empty, or whitespace only.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="enabledCategories"/> is <c>null</c>.
    /// </exception>
    public static Profile Of(string name, IEnumerable<string?> enabledCategories)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(enabledCategories);

        var deduplicated = new List<string?>();
        foreach (var category in enabledCategories)
        {
            if (!deduplicated.Any(existing => SameCategory(existing, category)))
            {
                deduplicated.Add(category);
            }
        }

        return new Profile(name.Trim(), deduplicated);
    }

    private static bool SameCategory(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);
}
