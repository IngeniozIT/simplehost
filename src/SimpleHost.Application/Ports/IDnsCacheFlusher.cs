namespace SimpleHost.Application.Ports;

/// <summary>
/// Port letting the application clear the operating system's DNS resolver cache. Implemented by an
/// infrastructure adapter that asks the OS to flush its cache; the application uses it, after a
/// successful save, to make freshly written <c>hosts</c>-file changes take effect immediately
/// instead of waiting for previously cached lookups to expire.
/// </summary>
public interface IDnsCacheFlusher
{
    /// <summary>
    /// Flushes the operating system's DNS resolver cache, discarding cached hostname lookups so the
    /// next resolution reads the current <c>hosts</c> file. Called only when the user opted in and
    /// only after a save that actually wrote changes.
    /// </summary>
    void Flush();
}
