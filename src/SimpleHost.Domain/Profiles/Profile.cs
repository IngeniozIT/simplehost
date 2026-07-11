using SimpleHost.Domain.Categories;

namespace SimpleHost.Domain.Profiles;

/// <summary>
/// A named, saved preset that can be recalled to switch the <c>hosts</c> file to a remembered state.
/// A profile comes in one of two shapes. A <b>category preset</b> — for example <c>"Work"</c> or
/// <c>"Home"</c> — records only which categories should be enabled; recalling it enables those
/// categories and disables every other one. A <b>snapshot</b> — such as the built-in <c>"Default"</c>
/// — instead captures the whole original content of the file, so recalling it restores the file to
/// exactly how it was when the snapshot was taken, undoing any entries added, edited, deleted, moved
/// or reordered since.
/// </summary>
/// <remarks>
/// A category preset's enabled categories are identified by name, matched exactly (case-sensitively)
/// as everywhere else in the domain, with <c>null</c> standing for the special "No category" group.
/// The set is deduplicated on construction — listing the same category twice is a no-op — and kept in
/// the order the categories were first given. An empty set is allowed: it is a preset that disables
/// everything. A snapshot instead carries the raw file content it restores, together with that
/// content already grouped by category, so a recall can both preview the restored grouping and write
/// the original content back verbatim. <see cref="Snapshot"/> being non-<c>null</c> is what tells the
/// two shapes apart.
/// </remarks>
public sealed record Profile
{
    private Profile(
        string name,
        IReadOnlyList<string?> enabledCategories,
        string? snapshot,
        IReadOnlyList<CategoryGroup>? snapshotGroups)
    {
        Name = name;
        EnabledCategories = enabledCategories;
        Snapshot = snapshot;
        SnapshotGroups = snapshotGroups;
    }

    /// <summary>The profile's name, as it is shown and recalled by. Never empty or whitespace.</summary>
    public string Name { get; }

    /// <summary>
    /// The categories to enable when a category preset is recalled, deduplicated and in the order they
    /// were first given. <c>null</c> stands for the "No category" group. Every category not in this
    /// set is meant to be disabled. May be empty, and is always empty for a snapshot profile.
    /// </summary>
    public IReadOnlyList<string?> EnabledCategories { get; }

    /// <summary>
    /// The raw <c>hosts</c> file content this profile restores verbatim when recalled, or <c>null</c>
    /// when the profile is a category preset rather than a snapshot. This is what makes reverting to
    /// the original file exact — the whole content is written back, comments and blank lines included,
    /// not just the entries' enabled state.
    /// </summary>
    public string? Snapshot { get; }

    /// <summary>
    /// The captured content of <see cref="Snapshot"/> already grouped by category — the "No category"
    /// group first, then each category in file order — so a recall can show the restored grouping in
    /// the pending-changes preview without re-parsing the raw content. <c>null</c> for a category
    /// preset. Present exactly when <see cref="Snapshot"/> is.
    /// </summary>
    public IReadOnlyList<CategoryGroup>? SnapshotGroups { get; }

    /// <summary>
    /// Whether recalling this profile restores a captured snapshot of the whole file (rather than
    /// enabling and disabling categories). <see langword="true"/> exactly when <see cref="Snapshot"/>
    /// is non-<c>null</c>.
    /// </summary>
    public bool RestoresSnapshot => Snapshot is not null;

    /// <summary>
    /// Creates a category-preset profile named <paramref name="name"/> — trimmed of surrounding
    /// whitespace — that enables the given <paramref name="enabledCategories"/>. Duplicate categories
    /// (matched exactly, case-sensitively, with <c>null</c> meaning "No category") are collapsed to a
    /// single entry, keeping the order of first appearance.
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

        return new Profile(name.Trim(), deduplicated, snapshot: null, snapshotGroups: null);
    }

    /// <summary>
    /// Creates a snapshot profile named <paramref name="name"/> — trimmed of surrounding whitespace —
    /// that restores <paramref name="snapshot"/>, the raw <c>hosts</c> file content captured verbatim,
    /// when recalled. <paramref name="snapshotGroups"/> is that same content already grouped by
    /// category, used to preview the restore. The snapshot content itself is kept exactly as given,
    /// never trimmed, so the file is restored byte for byte.
    /// </summary>
    /// <param name="name">
    /// The name to save the profile under. Surrounding whitespace is trimmed off; the name must not
    /// be empty or whitespace only.
    /// </param>
    /// <param name="snapshot">The raw file content to restore verbatim on recall.</param>
    /// <param name="snapshotGroups">
    /// The captured content grouped by category, in file order, used to preview the restored state.
    /// </param>
    /// <returns>The snapshot profile, with its name trimmed and its captured content preserved.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is <c>null</c>, empty, or whitespace only.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="snapshot"/> or <paramref name="snapshotGroups"/> is <c>null</c>.
    /// </exception>
    public static Profile FromSnapshot(
        string name, string snapshot, IEnumerable<CategoryGroup> snapshotGroups)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshotGroups);

        return new Profile(
            name.Trim(), Array.Empty<string?>(), snapshot, snapshotGroups.ToList());
    }

    /// <summary>
    /// Returns a copy of this profile saved under <paramref name="newName"/> — trimmed of surrounding
    /// whitespace — keeping everything else it records unchanged, so recalling the renamed profile
    /// applies or restores exactly what it did before. A category preset keeps its enabled categories
    /// (in order) and a snapshot keeps the captured content and its grouping; only the name changes.
    /// </summary>
    /// <param name="newName">
    /// The name to save the profile under instead. Surrounding whitespace is trimmed off; the name
    /// must not be empty or whitespace only, since a profile has to be named to be recalled.
    /// </param>
    /// <returns>A profile identical to this one but named <paramref name="newName"/> (trimmed).</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="newName"/> is <c>null</c>, empty, or whitespace only.
    /// </exception>
    public Profile Renamed(string newName)
        => RestoresSnapshot
            ? FromSnapshot(newName, Snapshot!, SnapshotGroups!)
            : Of(newName, EnabledCategories);

    private static bool SameCategory(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);
}
