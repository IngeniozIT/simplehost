namespace SimpleHost.Application.Startup;

/// <summary>
/// Decides which <c>hosts</c> file the app works against for a run, letting it target any file — a
/// fake or sample file for local testing — rather than only the protected system file. The active
/// file is resolved from, in order of precedence: a <c>--hosts-file &lt;path&gt;</c> command-line
/// argument, then the <c>SIMPLEHOST_HOSTS_FILE</c> environment variable, then the default Windows
/// system path <c>C:\Windows\System32\drivers\etc\hosts</c>. The result also records whether the
/// chosen file is the protected system file, so the caller knows a write needs administrator
/// elevation, or a custom file, which needs none.
/// <para>
/// The logic lives in the testable application layer, off the WPF shell: the command line arrives as
/// a plain argument list and the environment as an injected lookup, so resolution is exercised end
/// to end without touching the real process environment. The composition root supplies the process
/// arguments and <c>Environment.GetEnvironmentVariable</c> as the lookup.
/// </para>
/// </summary>
public sealed class HostsFilePathResolver
{
    /// <summary>
    /// The default Windows system <c>hosts</c> file path, used when neither the command-line argument
    /// nor the environment variable selects a file. Writing to this file requires administrator
    /// elevation.
    /// </summary>
    public const string DefaultWindowsHostsFilePath = @"C:\Windows\System32\drivers\etc\hosts";

    private const string HostsFileOption = "--hosts-file";
    private const string EnvironmentVariableName = "SIMPLEHOST_HOSTS_FILE";

    /// <summary>
    /// Resolves the active <c>hosts</c> file for this run. Prefers a non-blank
    /// <c>--hosts-file &lt;path&gt;</c> value from <paramref name="args"/>; failing that, a non-blank
    /// <c>SIMPLEHOST_HOSTS_FILE</c> value from <paramref name="environmentVariable"/>; failing that,
    /// the default system path. A chosen path is trimmed of surrounding whitespace, and the result
    /// flags whether it is the protected system file.
    /// </summary>
    /// <param name="args">
    /// The process command-line arguments. The value following <c>--hosts-file</c> (matched
    /// case-insensitively) is used; when that option is absent, ends the arguments, is immediately
    /// followed by another <c>--</c> option, or carries only whitespace, no file is taken from the
    /// command line.
    /// </param>
    /// <param name="environmentVariable">
    /// Looks up an environment variable by name, returning its value or <c>null</c> when it is not
    /// set. Called with <c>SIMPLEHOST_HOSTS_FILE</c>; a <c>null</c> or whitespace-only value is
    /// treated as unset.
    /// </param>
    /// <returns>The resolved <c>hosts</c> file path and whether it is the protected system file.</returns>
    public ResolvedHostsFile Resolve(IReadOnlyList<string> args, Func<string, string?> environmentVariable)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environmentVariable);

        var path = ArgumentValue(args)
            ?? EnvironmentValue(environmentVariable)
            ?? DefaultWindowsHostsFilePath;

        return new ResolvedHostsFile(path, IsProtectedSystemFile(path));
    }

    /// <summary>
    /// Returns the trimmed, non-blank value following the first <c>--hosts-file</c> option in
    /// <paramref name="args"/>, or <c>null</c> when the option is absent, ends the arguments, is
    /// immediately followed by another <c>--</c> option, or carries only whitespace.
    /// </summary>
    private static string? ArgumentValue(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            if (!string.Equals(args[index], HostsFileOption, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= args.Count)
            {
                return null;
            }

            var value = args[index + 1];
            if (value.StartsWith("--", StringComparison.Ordinal))
            {
                return null;
            }

            return Cleaned(value);
        }

        return null;
    }

    /// <summary>
    /// Returns the trimmed, non-blank <c>SIMPLEHOST_HOSTS_FILE</c> value, or <c>null</c> when it is
    /// unset or whitespace only.
    /// </summary>
    private static string? EnvironmentValue(Func<string, string?> environmentVariable)
        => Cleaned(environmentVariable(EnvironmentVariableName));

    /// <summary>
    /// Trims surrounding whitespace from <paramref name="value"/>, returning <c>null</c> when it is
    /// absent or whitespace only.
    /// </summary>
    private static string? Cleaned(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Reports whether <paramref name="path"/> denotes the protected Windows system <c>hosts</c>
    /// file, comparing case-insensitively and treating forward and back slashes alike.
    /// </summary>
    private static bool IsProtectedSystemFile(string path)
        => string.Equals(Normalize(path), Normalize(DefaultWindowsHostsFilePath), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Normalizes a path for comparison by treating forward slashes as back slashes, so the same file
    /// written with either separator compares equal.
    /// </summary>
    private static string Normalize(string path) => path.Replace('/', '\\');
}
