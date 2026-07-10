using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;
using SimpleHost.Application.Ports;

namespace SimpleHost.Infrastructure.Appearance;

/// <summary>
/// Windows adapter for <see cref="ISystemThemeProvider"/> that reports whether the operating system is
/// set to show apps in light mode and signals when the user changes that setting. The current value is
/// read from the personalization setting Windows keeps under
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme</c> by running
/// the system <c>reg</c> tool, mirroring how <c>WindowsUrlSchemeRegistrar</c> and
/// <c>WindowsDnsCacheFlusher</c> shell out to <c>reg</c> and <c>ipconfig</c>; when the value is missing
/// Windows shows apps in light mode, so that is the default. Changes are picked up from
/// <see cref="SystemEvents.UserPreferenceChanged"/> — the operating-system notification that fires when
/// personalization settings such as the app theme change, and the reason this adapter takes the
/// Windows-only <c>Microsoft.Win32.SystemEvents</c> dependency (there is no shell command to subscribe
/// to a live setting change). This is a thin wrapper over the Windows personalization boundary — exactly
/// the kind of external dependency the project's testing rules cover with a double rather than exercise
/// directly — so it is excluded from coverage; the application-layer <c>SystemThemeWatcher</c> that
/// resolves and follows the theme is what carries the behaviour and its tests.
/// </summary>
[SupportedOSPlatform("windows")]
[ExcludeFromCodeCoverage]
public sealed class WindowsSystemThemeProvider : ISystemThemeProvider, IDisposable
{
    private const string PersonalizeKey =
        @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    /// <summary>
    /// Starts reporting the system theme and listening for the user changing it.
    /// </summary>
    public WindowsSystemThemeProvider()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <inheritdoc />
    public event EventHandler? ThemeChanged;

    /// <inheritdoc />
    public bool IsLightTheme => ReadAppsUseLightTheme();

    /// <summary>Stops listening for the user changing the system theme.</summary>
    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool ReadAppsUseLightTheme()
    {
        var output = Query(PersonalizeKey, AppsUseLightThemeValue);

        // reg prints a line like: "    AppsUseLightTheme    REG_DWORD    0x1". The value is 0x1 for
        // light and 0x0 for dark; when the value or key is absent Windows shows apps in light mode.
        var marker = output.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
        {
            return true;
        }

        var digits = output.AsSpan(marker + 2);
        var length = 0;
        while (length < digits.Length && Uri.IsHexDigit(digits[length]))
        {
            length++;
        }

        return !int.TryParse(digits[..length], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            || value != 0;
    }

    private static string Query(string key, string value)
    {
        var startInfo = new ProcessStartInfo("reg")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };
        startInfo.ArgumentList.Add("query");
        startInfo.ArgumentList.Add(key);
        startInfo.ArgumentList.Add("/v");
        startInfo.ArgumentList.Add(value);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return string.Empty;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
