using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Ports;

namespace SimpleHost.Infrastructure.DeepLinks;

/// <summary>
/// Windows adapter for <see cref="IUrlSchemeRegistrar"/> that registers the <c>simplehost://</c> URL
/// scheme under the current user's registry (<c>HKCU\Software\Classes</c>) by running the system
/// <c>reg.exe</c> tool, mirroring how <c>WindowsDnsCacheFlusher</c> shells out to <c>ipconfig</c>.
/// Writing under <c>HKCU</c> needs no elevation and points the scheme at the app so the operating
/// system launches it — passing the opened link as the argument — for every <c>simplehost://</c> URL.
/// This is a thin wrapper over the Windows registry boundary — exactly the kind of external
/// dependency the project's testing rules cover with a double rather than exercise directly — so it
/// is excluded from coverage; the parsing of the link the app then receives is what carries the
/// tested behaviour.
/// </summary>
[SupportedOSPlatform("windows")]
[ExcludeFromCodeCoverage]
public sealed class WindowsUrlSchemeRegistrar : IUrlSchemeRegistrar
{
    /// <inheritdoc />
    public void Register(string executablePath)
    {
        var schemeKey = $@"HKCU\Software\Classes\{DeepLink.Scheme}";

        // (Default) = "URL:SimpleHost Protocol" and the "URL Protocol" marker together tell Windows
        // this key is a launchable URL scheme handler.
        Reg("add", schemeKey, "/ve", "/d", $"URL:{DeepLink.Scheme} Protocol", "/f");
        Reg("add", schemeKey, "/v", "URL Protocol", "/d", string.Empty, "/f");

        // shell\open\command's default value is the command line Windows runs, with %1 replaced by
        // the opened link.
        Reg("add", $@"{schemeKey}\shell\open\command", "/ve", "/d", $"\"{executablePath}\" \"%1\"", "/f");
    }

    private static void Reg(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("reg")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        process?.WaitForExit();
    }
}
