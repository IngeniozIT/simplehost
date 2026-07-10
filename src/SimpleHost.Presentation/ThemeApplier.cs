using System.Diagnostics.CodeAnalysis;
using System.Windows;
using SimpleHost.Application.Appearance;

namespace SimpleHost.Presentation;

/// <summary>
/// Applies the <see cref="AppTheme"/> the app should show by swapping the matching light or dark
/// resource dictionary into the application's resources, so every control bound to the theme brushes
/// (through <c>DynamicResource</c>) repaints in the new appearance. Kept a thin shell over WPF — the
/// decision of <em>which</em> theme to show lives in the application layer's <c>SystemThemeWatcher</c>;
/// this only carries that decision to the on-screen resources.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ThemeApplier
{
    private readonly System.Windows.Application _application;
    private ResourceDictionary? _current;

    /// <summary>
    /// Creates an applier that swaps theme dictionaries on <paramref name="application"/>'s resources.
    /// </summary>
    /// <param name="application">The running WPF application whose resources hold the theme brushes.</param>
    public ThemeApplier(System.Windows.Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _application = application;
    }

    /// <summary>
    /// Shows <paramref name="theme"/> by replacing any previously applied theme dictionary with the one
    /// for the requested appearance.
    /// </summary>
    /// <param name="theme">The appearance to show.</param>
    public void Apply(AppTheme theme)
    {
        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"Themes/{theme}.xaml", UriKind.Relative),
        };

        var merged = _application.Resources.MergedDictionaries;
        if (_current is not null)
        {
            merged.Remove(_current);
        }

        merged.Add(dictionary);
        _current = dictionary;
    }
}
