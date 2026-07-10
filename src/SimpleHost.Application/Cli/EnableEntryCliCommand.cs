using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;

namespace SimpleHost.Application.Cli;

/// <summary>
/// The command-line companion's <c>enable</c> command: it lets a user re-enable a disabled host entry
/// from a terminal, mirroring the <c>simplehost://enable</c> deep-link action. It reads a
/// <c>--hostname</c> (which identifies the entry to re-enable) from the command-line arguments and
/// resolves it against the current entries. Re-enabling is non-destructive — it makes a
/// kept-but-not-resolving entry resolve again — so, unlike the disable command, it needs no
/// confirmation prompt; recording it as a pending change is what keeps the user in control.
/// <para>
/// To guarantee the terminal and a <c>simplehost://enable</c> link behave identically, the parsed
/// option is turned into an enable deep link and handed to the same
/// <see cref="EnableEntryDeepLinkHandler"/>: the entry is matched by hostname case-insensitively,
/// taking the first <em>disabled</em> entry in file order, and a command that names no hostname,
/// matches no entry, or matches only an already-active entry resolves nothing. A resolved re-enable is
/// recorded straight into the <see cref="PendingChangeSet"/> for the user to review.
/// </para>
/// </summary>
public sealed class EnableEntryCliCommand
{
    /// <summary>
    /// Reads the <c>--hostname</c> option from <paramref name="args"/>, resolves the described
    /// re-enable against <paramref name="currentEntries"/>, and records it in
    /// <paramref name="pendingChanges"/>. Returns the resolved <see cref="EnableEntryDeepLinkResult"/>:
    /// the re-enabled entry when a disabled one was matched, or a rejection explaining why nothing
    /// could be enabled. When the re-enable is rejected, nothing is recorded.
    /// </summary>
    /// <param name="args">
    /// The command-line arguments for the <c>enable</c> command, as <c>--name value</c> option pairs.
    /// Option names are matched case-insensitively; an option whose value is another option or that
    /// ends the arguments takes an empty value, and unrecognised options and stray non-option tokens
    /// are ignored.
    /// </param>
    /// <param name="currentEntries">
    /// The current entries, grouped by category, that the <c>--hostname</c> is matched against.
    /// </param>
    /// <param name="pendingChanges">The set the resolved re-enable is recorded in.</param>
    public EnableEntryDeepLinkResult Execute(
        IReadOnlyList<string> args,
        IReadOnlyList<CategoryGroup> currentEntries,
        PendingChangeSet pendingChanges)
    {
        ArgumentNullException.ThrowIfNull(args);

        var options = ParseOptions(args);

        var link = BuildEnableLink(options);
        DeepLink.TryParse(link, out var parsed);

        return new EnableEntryDeepLinkHandler().Handle(parsed!, currentEntries, pendingChanges);
    }

    /// <summary>
    /// Turns the recognised options into an equivalent <c>simplehost://enable</c> deep link, including
    /// only the options that were supplied so a missing one is seen as absent by the handler, with the
    /// value percent-encoded so it survives being carried in the link's query string.
    /// </summary>
    private static string BuildEnableLink(IReadOnlyDictionary<string, string> options)
    {
        var query = new List<string>();
        foreach (var name in new[] { "hostname" })
        {
            if (options.TryGetValue(name, out var value))
            {
                query.Add($"{name}={Uri.EscapeDataString(value)}");
            }
        }

        return $"simplehost://enable?{string.Join('&', query)}";
    }

    /// <summary>
    /// Parses <paramref name="args"/> into a case-insensitive map of <c>--name</c> options to the value
    /// in the following token. An option whose next token is itself an option, or that ends the
    /// arguments, takes an empty value; tokens that are not <c>--</c> options are skipped.
    /// </summary>
    private static IReadOnlyDictionary<string, string> ParseOptions(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var name = token[2..];
            var value = string.Empty;
            if (index + 1 < args.Count
                && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++index];
            }

            options[name] = value;
        }

        return options;
    }
}
