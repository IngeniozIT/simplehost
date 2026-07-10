namespace SimpleHost.Application.Startup;

/// <summary>
/// The <c>hosts</c> file the app has decided to work against for this run, together with whether it
/// is the protected system file. Produced by <see cref="HostsFilePathResolver"/> from the process
/// command line and environment so the composition root knows which file to read and write and
/// whether saving to it requires administrator elevation.
/// </summary>
/// <param name="Path">
/// The full path of the <c>hosts</c> file to work against — a custom file chosen for local testing,
/// or the default Windows system <c>hosts</c> file when nothing else was specified.
/// </param>
/// <param name="IsProtectedSystemFile">
/// <c>true</c> when <paramref name="Path"/> is the protected Windows system <c>hosts</c> file, so
/// writing to it requires administrator elevation; <c>false</c> for a custom file, which can be
/// written without elevation.
/// </param>
public sealed record ResolvedHostsFile(string Path, bool IsProtectedSystemFile);
