using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;

namespace SimpleHost.Application.Cli;

/// <summary>
/// The command-line companion's <c>disable</c> command: it lets a user stop an existing host entry
/// resolving from a terminal, mirroring the <c>simplehost://disable</c> deep-link action. It reads a
/// <c>--hostname</c> (which identifies the entry to disable) from the command-line arguments, resolves
/// it against the current entries, and — because disabling an entry stops it resolving — stages the
/// change as a pending update only after the caller's confirmation.
/// <para>
/// To guarantee the terminal and a <c>simplehost://disable</c> link behave identically, the parsed
/// option is turned into a disable deep link and handed to the same
/// <see cref="DisableEntryDeepLinkHandler"/>: the entry is matched by hostname case-insensitively,
/// taking the first <em>active</em> entry in file order, and a command that names no hostname, matches
/// no entry, or matches only an already-disabled entry resolves nothing. A resolved disable is
/// surfaced to <paramref name="confirm" /> and staged in the <see cref="PendingChangeSet"/> only when
/// it answers <c>true</c>, so the change is always gated on the user's consent.
/// </para>
/// </summary>
public sealed class DisableEntryCliCommand
{
    /// <summary>
    /// Reads the <c>--hostname</c> option from <paramref name="args"/>, resolves the described disable
    /// against <paramref name="currentEntries"/>, and — after <paramref name="confirm"/> approves it —
    /// records the pending update in <paramref name="pendingChanges"/>. Returns the resolved
    /// <see cref="DisableEntryDeepLinkResult"/>: a confirmation-required disable when an active entry
    /// was matched, or a rejection explaining why nothing could be disabled. When the disable is
    /// rejected, or <paramref name="confirm"/> declines it, nothing is recorded.
    /// </summary>
    /// <param name="args">
    /// The command-line arguments for the <c>disable</c> command, as <c>--name value</c> option pairs.
    /// Option names are matched case-insensitively; an option whose value is another option or that
    /// ends the arguments takes an empty value, and unrecognised options and stray non-option tokens
    /// are ignored.
    /// </param>
    /// <param name="currentEntries">
    /// The current entries, grouped by category, that the <c>--hostname</c> is matched against.
    /// </param>
    /// <param name="pendingChanges">The set the confirmed disable is recorded in.</param>
    /// <param name="confirm">
    /// Asked to approve a resolved disable before it is staged; the change is recorded only when it
    /// returns <c>true</c>. It is not consulted when nothing was resolved.
    /// </param>
    public DisableEntryDeepLinkResult Execute(
        IReadOnlyList<string> args,
        IReadOnlyList<CategoryGroup> currentEntries,
        PendingChangeSet pendingChanges,
        Func<DisableEntryDeepLinkResult, bool> confirm)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(pendingChanges);
        ArgumentNullException.ThrowIfNull(confirm);

        var options = ParseOptions(args);

        var link = BuildDisableLink(options);
        DeepLink.TryParse(link, out var parsed);

        var result = new DisableEntryDeepLinkHandler().Handle(parsed!, currentEntries);

        if (result.RequiresConfirmation && confirm(result))
        {
            result.RecordInto(pendingChanges);
        }

        return result;
    }

    /// <summary>
    /// Turns the recognised options into an equivalent <c>simplehost://disable</c> deep link, including
    /// only the options that were supplied so a missing one is seen as absent by the handler, with the
    /// value percent-encoded so it survives being carried in the link's query string.
    /// </summary>
    private static string BuildDisableLink(IReadOnlyDictionary<string, string> options)
    {
        var query = new List<string>();
        foreach (var name in new[] { "hostname" })
        {
            if (options.TryGetValue(name, out var value))
            {
                query.Add($"{name}={Uri.EscapeDataString(value)}");
            }
        }

        return $"simplehost://disable?{string.Join('&', query)}";
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
