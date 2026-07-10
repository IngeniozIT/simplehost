using SimpleHost.Application.Ports;

namespace SimpleHost.Application.Appearance;

/// <summary>
/// Keeps SimpleHost's appearance following the operating system's light/dark theme. It resolves the
/// system setting reported by an <see cref="ISystemThemeProvider"/> into the <see cref="AppTheme"/> the
/// app should show, remembers the theme currently in effect, and — whenever the system setting changes
/// to a different theme — raises <see cref="ThemeChanged"/> so the UI can switch to match. A system
/// notification that leaves the resolved theme unchanged is ignored, so the app never churns its
/// appearance for a preference change that is not about the theme.
/// </summary>
public sealed class SystemThemeWatcher : IDisposable
{
    private readonly ISystemThemeProvider _provider;
    private AppTheme _currentTheme;

    /// <summary>
    /// Starts following the system theme through <paramref name="provider"/>: the <see cref="CurrentTheme"/>
    /// is set from the system's current setting and the watcher begins listening for later changes.
    /// </summary>
    /// <param name="provider">The system-theme source to read the current setting from and follow.</param>
    public SystemThemeWatcher(ISystemThemeProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _provider = provider;
        _currentTheme = Resolve();
        _provider.ThemeChanged += OnSystemThemeChanged;
    }

    /// <summary>
    /// Raised when the app should switch appearance because the system theme changed to a different one,
    /// carrying the <see cref="AppTheme"/> to show. Not raised for a system notification that leaves the
    /// resolved theme the same.
    /// </summary>
    public event Action<AppTheme>? ThemeChanged;

    /// <summary>The <see cref="AppTheme"/> the app should currently show to match the system setting.</summary>
    public AppTheme CurrentTheme => _currentTheme;

    /// <summary>Stops following the system theme, releasing the subscription to the provider.</summary>
    public void Dispose() => _provider.ThemeChanged -= OnSystemThemeChanged;

    private void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        var resolved = Resolve();
        if (resolved == _currentTheme)
        {
            return;
        }

        _currentTheme = resolved;
        ThemeChanged?.Invoke(resolved);
    }

    private AppTheme Resolve() => _provider.IsLightTheme ? AppTheme.Light : AppTheme.Dark;
}
