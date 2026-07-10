namespace SimpleHost.Application.Ports;

/// <summary>
/// Port telling the application whether the current process is running with the administrator
/// (elevated) rights needed to write the protected system <c>hosts</c> file. Implemented by an
/// infrastructure adapter that queries the operating system; the application uses it to guard the
/// single write step so changes only reach the real file when the app has the rights to save them.
/// </summary>
public interface IElevationService
{
    /// <summary>
    /// <c>true</c> when the process holds administrator (elevated) rights and may write the
    /// protected system <c>hosts</c> file; <c>false</c> when it does not and must first request
    /// elevation before any change can be saved.
    /// </summary>
    bool IsElevated { get; }
}
