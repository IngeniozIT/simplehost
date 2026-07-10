using SimpleHost.Application.Ports;

namespace SimpleHost.Presentation.Composition;

/// <summary>
/// The elevation service used when SimpleHost operates on a <c>hosts</c> file that is <b>not</b> the
/// protected system file — a local sample selected with <c>--hosts-file</c> or the
/// <c>SIMPLEHOST_HOSTS_FILE</c> environment variable. Such a file lives in a user-writable location,
/// so writing it never needs administrator rights; this adapter therefore reports the write step as
/// always permitted, which means the app imposes no elevation gate (and triggers no UAC prompt) when
/// pointed at a sample file.
/// </summary>
/// <remarks>
/// Elevation only matters for the protected system <c>hosts</c> file, which is guarded by the real
/// <see cref="SimpleHost.Infrastructure.Elevation.WindowsElevationService"/>. See
/// <see cref="CompositionRoot"/> for where the choice between the two is made.
/// </remarks>
internal sealed class WritableFileElevationService : IElevationService
{
    /// <summary>
    /// Always <c>true</c>: a user-writable <c>hosts</c> file may be written without elevation, so the
    /// application's write gate never blocks for these files.
    /// </summary>
    public bool IsElevated => true;
}
