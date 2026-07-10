using System.Collections.Generic;
using System.Linq;
using SimpleHost.Domain.Categories;

namespace SimpleHost.Presentation.ViewModels;

/// <summary>
/// A category and its host entries as presented to the window. Projects a domain
/// <see cref="CategoryGroup"/>, surfacing the "No category" group (whose
/// <see cref="CategoryGroup.Name"/> is <c>null</c>) under a stable display name so the view can
/// render it pinned to the top.
/// </summary>
public sealed class CategoryGroupViewModel
{
    /// <summary>The label shown for the pinned "No category" group.</summary>
    public const string NoCategoryDisplayName = "No category";

    public CategoryGroupViewModel(CategoryGroup group)
    {
        Name = group.Name;
        Entries = group.Entries.Select(entry => new HostEntryViewModel(entry)).ToList();
    }

    /// <summary>
    /// Projects a category from already-built entry view-models rather than domain entries, so a
    /// filtered view of an existing group can reuse the very same <see cref="HostEntryViewModel"/>
    /// instances — keeping selection and command identity intact across the underlying and the
    /// narrowed views.
    /// </summary>
    public CategoryGroupViewModel(string? name, IReadOnlyList<HostEntryViewModel> entries)
    {
        Name = name;
        Entries = entries;
    }

    /// <summary>The category name, or <c>null</c> for the "No category" group.</summary>
    public string? Name { get; }

    /// <summary>
    /// The name to display: the category's name, or <see cref="NoCategoryDisplayName"/> for the
    /// uncategorized group.
    /// </summary>
    public string DisplayName => Name ?? NoCategoryDisplayName;

    /// <summary>Whether this is the special "No category" group pinned to the top.</summary>
    public bool IsUncategorized => Name is null;

    /// <summary>The entries belonging to this category, in file order.</summary>
    public IReadOnlyList<HostEntryViewModel> Entries { get; }
}
