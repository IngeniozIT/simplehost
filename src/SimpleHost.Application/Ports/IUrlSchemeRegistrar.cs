namespace SimpleHost.Application.Ports;

/// <summary>
/// Port for registering SimpleHost as the operating-system handler for the <c>simplehost://</c> URL
/// scheme, so that opening such a link launches the app. Implemented by an infrastructure adapter
/// that records the registration with the OS (on Windows, in the registry); the app calls it so that
/// <c>simplehost://</c> links opened by a browser or another program are routed to SimpleHost.
/// </summary>
public interface IUrlSchemeRegistrar
{
    /// <summary>
    /// Registers <paramref name="executablePath"/> as the program the operating system launches when
    /// a <c>simplehost://</c> link is opened, passing the opened link to it as an argument. Safe to
    /// call repeatedly: an existing registration is overwritten so it always points at the current
    /// executable.
    /// </summary>
    /// <param name="executablePath">The full path to the SimpleHost executable to launch.</param>
    void Register(string executablePath);
}
