namespace SimpleHost.Application.Ports;

/// <summary>
/// Port reporting the operating system's current light/dark app theme so SimpleHost can match its own
/// appearance to it. Implemented by an infrastructure adapter that reads the Windows personalization
/// setting and listens for the user changing it; the application uses it to keep the app's appearance
/// following the system theme, switching whenever the system setting does.
/// </summary>
public interface ISystemThemeProvider
{
    /// <summary>
    /// <c>true</c> when the operating system is currently set to show apps in light mode; <c>false</c>
    /// when it is set to dark mode.
    /// </summary>
    bool IsLightTheme { get; }

    /// <summary>
    /// Raised when the operating system's light/dark setting may have changed, so a listener can
    /// re-read <see cref="IsLightTheme"/> and follow the new setting.
    /// </summary>
    event EventHandler? ThemeChanged;
}
