using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;

namespace SimpleHost.Application.Cli;

/// <summary>
/// The command-line companion's <c>update</c> command: it lets a user change an existing host entry's
/// IP address and/or category from a terminal, mirroring the <c>simplehost://update</c> deep-link
/// action. It reads a <c>--hostname</c> (which identifies the entry to change) plus an optional
/// <c>--ip</c> and/or <c>--category</c> from the command-line arguments, resolves them against the
/// current entries, and — because replacing an entry's address is destructive — stages the change as a
/// pending edit only after the caller's confirmation.
/// <para>
/// To guarantee the terminal and a <c>simplehost://update</c> link behave identically, the parsed
/// options are turned into an update deep link and handed to the same
/// <see cref="UpdateEntryDeepLinkHandler"/>: the entry is matched by hostname case-insensitively, a
/// malformed <c>--ip</c> is rejected and stages nothing, a blank <c>--ip</c> or <c>--category</c> is
/// treated as absent (leaving that facet unchanged), and a command that names no hostname, matches no
/// entry, or requests no actual change resolves nothing. A resolved change is surfaced to
/// <paramref name="confirm" /> and staged in the <see cref="PendingChangeSet"/> only when it answers
/// <c>true</c>, so the destructive edit is always gated on the user's consent.
/// </para>
/// </summary>
public sealed class UpdateEntryCliCommand
{
    /// <summary>
    /// Reads the <c>--hostname</c>, <c>--ip</c> and <c>--category</c> options from
    /// <paramref name="args"/>, resolves the described change against <paramref name="currentEntries"/>,
    /// and — after <paramref name="confirm"/> approves it — records the pending update and/or move in
    /// <paramref name="pendingChanges"/>. Returns the resolved <see cref="UpdateEntryDeepLinkResult"/>:
    /// a confirmation-required change when one was resolved, or a rejection explaining why nothing could
    /// be changed. When the change is rejected, or <paramref name="confirm"/> declines it, nothing is
    /// recorded.
    /// </summary>
    /// <param name="args">
    /// The command-line arguments for the <c>update</c> command, as <c>--name value</c> option pairs.
    /// Option names are matched case-insensitively; an option whose value is another option or that
    /// ends the arguments takes an empty value, and unrecognised options and stray non-option tokens
    /// are ignored.
    /// </param>
    /// <param name="currentEntries">
    /// The current entries, grouped by category, that the <c>--hostname</c> is matched against.
    /// </param>
    /// <param name="pendingChanges">The set the confirmed change is recorded in.</param>
    /// <param name="confirm">
    /// Asked to approve a resolved, destructive change before it is staged; the change is recorded only
    /// when it returns <c>true</c>. It is not consulted when nothing was resolved.
    /// </param>
    public UpdateEntryDeepLinkResult Execute(
        IReadOnlyList<string> args,
        IReadOnlyList<CategoryGroup> currentEntries,
        PendingChangeSet pendingChanges,
        Func<UpdateEntryDeepLinkResult, bool> confirm)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(confirm);

        var options = ParseOptions(args);

        var link = BuildUpdateLink(options);
        DeepLink.TryParse(link, out var parsed);

        var result = new UpdateEntryDeepLinkHandler().Handle(parsed!, currentEntries);

        if (result.RequiresConfirmation && confirm(result))
        {
            result.RecordInto(pendingChanges);
        }

        return result;
    }

    /// <summary>
    /// Turns the recognised options into an equivalent <c>simplehost://update</c> deep link, including
    /// only the options that were supplied so a missing one is seen as absent by the handler, with each
    /// value percent-encoded so it survives being carried in the link's query string.
    /// </summary>
    private static string BuildUpdateLink(IReadOnlyDictionary<string, string> options)
    {
        var query = new List<string>();
        foreach (var name in new[] { "hostname", "ip", "category" })
        {
            if (options.TryGetValue(name, out var value))
            {
                query.Add($"{name}={Uri.EscapeDataString(value)}");
            }
        }

        return $"simplehost://update?{string.Join('&', query)}";
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
