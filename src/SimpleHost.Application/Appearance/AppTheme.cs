namespace SimpleHost.Application.Appearance;

/// <summary>
/// The light or dark appearance SimpleHost shows. It mirrors the operating system's app theme so the
/// app looks at home next to the rest of Windows: <see cref="Light"/> when the system is set to show
/// apps in light mode, <see cref="Dark"/> when it is set to dark mode.
/// </summary>
public enum AppTheme
{
    /// <summary>The light appearance, shown when Windows is set to use light mode for apps.</summary>
    Light,

    /// <summary>The dark appearance, shown when Windows is set to use dark mode for apps.</summary>
    Dark,
}
