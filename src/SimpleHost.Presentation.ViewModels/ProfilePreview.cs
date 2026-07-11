using System.Collections.Generic;

namespace SimpleHost.Presentation.ViewModels;

/// <summary>
/// A read-only description of what recalling a saved profile would do, shown to the user before they
/// switch to it. It names the profile and lists the categories the switch concerns, using display
/// names (the "No category" group shown as <see cref="CategoryGroupViewModel.NoCategoryDisplayName"/>).
/// The two profile shapes are told apart by <see cref="RestoresSnapshot"/>: a category preset
/// <em>enables</em> the listed categories (and disables every other one), while a snapshot
/// <em>restores</em> the whole captured file, whose categories are the ones listed.
/// </summary>
/// <param name="Name">The profile's name, as it is saved and recalled by.</param>
/// <param name="RestoresSnapshot">
/// <see langword="true"/> when recalling the profile restores a captured snapshot of the whole file
/// (such as the built-in "Default"); <see langword="false"/> for a category preset that enables the
/// listed categories and disables the rest.
/// </param>
/// <param name="Categories">
/// The display names of the categories the recall concerns, in order: for a category preset the
/// categories it enables, and for a snapshot the categories present in the restored file. May be empty
/// — a preset that enables nothing, or a snapshot of an empty file.
/// </param>
public sealed record ProfilePreview(
    string Name, bool RestoresSnapshot, IReadOnlyList<string> Categories);
