namespace SimpleHost.Application.UseCases.ApplyPendingChanges;

/// <summary>
/// Raised when the user tries to apply pending changes while the process lacks the administrator
/// (elevated) rights required to write the protected system <c>hosts</c> file. Nothing is written
/// and the pending changes are left untouched, so the app can request elevation and let the user
/// retry the save once it is running as administrator.
/// </summary>
public sealed class ElevationRequiredException : Exception
{
    /// <summary>
    /// Creates the exception with a message explaining that administrator rights are required to
    /// save changes to the <c>hosts</c> file.
    /// </summary>
    public ElevationRequiredException()
        : base("Administrator rights are required to save changes to the hosts file.")
    {
    }
}
