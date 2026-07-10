using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Security.Principal;
using SimpleHost.Application.Ports;

namespace SimpleHost.Infrastructure.Elevation;

/// <summary>
/// Windows adapter for <see cref="IElevationService"/> that reports whether the current process is
/// running with administrator rights by asking the operating system through
/// <see cref="WindowsPrincipal"/>. This is a thin wrapper over an OS boundary — exactly the kind of
/// external dependency the project's testing rules cover with a double rather than exercise directly
/// (there is no way to fake the host's elevation state), so it is excluded from coverage; the
/// application-layer gate that consumes it is what carries the behaviour and its tests.
/// </summary>
[SupportedOSPlatform("windows")]
[ExcludeFromCodeCoverage]
public sealed class WindowsElevationService : IElevationService
{
    /// <inheritdoc />
    public bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
