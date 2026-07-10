using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;

namespace SimpleHost.Application.Cli;

/// <summary>
/// The command-line companion's <c>add</c> command: it lets a user record a new host entry from a
/// terminal, mirroring the <c>simplehost://add</c> deep-link action. It reads an <c>--ip</c>,
/// <c>--hostname</c> and optional <c>--category</c> / <c>--comment</c> from the command-line
/// arguments and records the described entry as a pending addition in the user's
/// <see cref="PendingChangeSet"/>, so the change is surfaced for review before anything is written
/// to the <c>hosts</c> file.
/// <para>
/// Adding is non-destructive, so — like the add deep link and unlike the update, delete and disable
/// commands — it needs no confirmation. To guarantee the terminal and a <c>simplehost://add</c> link
/// behave identically, the parsed options are turned into an add deep link and handed to the same
/// <see cref="AddEntryDeepLinkHandler"/>: the IP address and hostname are validated against the very
/// rules the editor enforces, a missing or malformed one is rejected and records nothing, and a
/// blank <c>--category</c> or <c>--comment</c> is treated as absent.
/// </para>
/// </summary>
public sealed class AddEntryCliCommand
{
    /// <summary>
    /// Reads the <c>--ip</c>, <c>--hostname</c>, <c>--category</c> and <c>--comment</c> options from
    /// <paramref name="args"/> and, when the IP address and hostname are both well-formed, records a
    /// pending addition of the described active entry in <paramref name="pendingChanges"/>, returning
    /// an added result carrying the created entry. When the IP address or hostname is missing or
    /// malformed, records nothing and returns a rejected result explaining why.
    /// </summary>
    /// <param name="args">
    /// The command-line arguments for the <c>add</c> command, as <c>--name value</c> option pairs.
    /// Option names are matched case-insensitively; an option whose value is another option or that
    /// ends the arguments takes an empty value, and unrecognised options and stray non-option tokens
    /// are ignored.
    /// </param>
    /// <param name="pendingChanges">The set the pending addition is recorded in.</param>
    public AddEntryDeepLinkResult Execute(IReadOnlyList<string> args, PendingChangeSet pendingChanges)
    {
        ArgumentNullException.ThrowIfNull(args);

        var options = ParseOptions(args);

        var link = BuildAddLink(options);
        DeepLink.TryParse(link, out var parsed);

        return new AddEntryDeepLinkHandler().Handle(parsed!, pendingChanges);
    }

    /// <summary>
    /// Turns the recognised options into an equivalent <c>simplehost://add</c> deep link, including
    /// only the options that were supplied so a missing one is seen as absent by the handler, with
    /// each value percent-encoded so it survives being carried in the link's query string.
    /// </summary>
    private static string BuildAddLink(IReadOnlyDictionary<string, string> options)
    {
        var query = new List<string>();
        foreach (var name in new[] { "ip", "hostname", "category", "comment" })
        {
            if (options.TryGetValue(name, out var value))
            {
                query.Add($"{name}={Uri.EscapeDataString(value)}");
            }
        }

        return $"simplehost://add?{string.Join('&', query)}";
    }

    /// <summary>
    /// Parses <paramref name="args"/> into a case-insensitive map of <c>--name</c> options to the
    /// value in the following token. An option whose next token is itself an option, or that ends the
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
