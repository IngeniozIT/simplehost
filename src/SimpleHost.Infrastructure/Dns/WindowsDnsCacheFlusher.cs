using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using SimpleHost.Application.Ports;

namespace SimpleHost.Infrastructure.Dns;

/// <summary>
/// Windows adapter for <see cref="IDnsCacheFlusher"/> that clears the operating system's DNS
/// resolver cache by running <c>ipconfig /flushdns</c>. This is a thin wrapper over an OS boundary —
/// exactly the kind of external dependency the project's testing rules cover with a double rather
/// than exercise directly (there is no way to observe the host's DNS cache from a unit test), so it
/// is excluded from coverage; the application-layer save step that decides whether to call it is
/// what carries the behaviour and its tests.
/// </summary>
[SupportedOSPlatform("windows")]
[ExcludeFromCodeCoverage]
public sealed class WindowsDnsCacheFlusher : IDnsCacheFlusher
{
    /// <inheritdoc />
    public void Flush()
    {
        var startInfo = new ProcessStartInfo("ipconfig", "/flushdns")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        process?.WaitForExit();
    }
}
