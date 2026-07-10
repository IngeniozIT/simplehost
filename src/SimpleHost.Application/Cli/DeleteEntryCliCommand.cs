using SimpleHost.Application.DeepLinks;
using SimpleHost.Application.Pending;
using SimpleHost.Domain.Categories;

namespace SimpleHost.Application.Cli;

/// <summary>
/// The command-line companion's <c>delete</c> command: it lets a user remove an existing host entry
/// from a terminal, mirroring the <c>simplehost://delete</c> deep-link action. It reads a
/// <c>--hostname</c> (which identifies the entry to remove) from the command-line arguments, resolves
/// it against the current entries, and — because removing an entry is destructive — stages the
/// deletion as a pending change only after the caller's confirmation.
/// <para>
/// To guarantee the terminal and a <c>simplehost://delete</c> link behave identically, the parsed
/// option is turned into a delete deep link and handed to the same
/// <see cref="DeleteEntryDeepLinkHandler"/>: the entry is matched by hostname case-insensitively,
/// taking the first match in file order, and a command that names no hostname or matches no entry
/// resolves nothing. A resolved deletion is surfaced to <paramref name="confirm" /> and staged in the
/// <see cref="PendingChangeSet"/> only when it answers <c>true</c>, so the destructive removal is
/// always gated on the user's consent.
/// </para>
/// </summary>
public sealed class DeleteEntryCliCommand
{
    /// <summary>
    /// Reads the <c>--hostname</c> option from <paramref name="args"/>, resolves the described
    /// deletion against <paramref name="currentEntries"/>, and — after <paramref name="confirm"/>
    /// approves it — records the pending removal in <paramref name="pendingChanges"/>. Returns the
    /// resolved <see cref="DeleteEntryDeepLinkResult"/>: a confirmation-required deletion when an entry
    /// was matched, or a rejection explaining why nothing could be deleted. When the deletion is
    /// rejected, or <paramref name="confirm"/> declines it, nothing is recorded.
    /// </summary>
    /// <param name="args">
    /// The command-line arguments for the <c>delete</c> command, as <c>--name value</c> option pairs.
    /// Option names are matched case-insensitively; an option whose value is another option or that
    /// ends the arguments takes an empty value, and unrecognised options and stray non-option tokens
    /// are ignored.
    /// </param>
    /// <param name="currentEntries">
    /// The current entries, grouped by category, that the <c>--hostname</c> is matched against.
    /// </param>
    /// <param name="pendingChanges">The set the confirmed deletion is recorded in.</param>
    /// <param name="confirm">
    /// Asked to approve a resolved, destructive deletion before it is staged; the removal is recorded
    /// only when it returns <c>true</c>. It is not consulted when nothing was resolved.
    /// </param>
    public DeleteEntryDeepLinkResult Execute(
        IReadOnlyList<string> args,
        IReadOnlyList<CategoryGroup> currentEntries,
        PendingChangeSet pendingChanges,
        Func<DeleteEntryDeepLinkResult, bool> confirm)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(pendingChanges);
        ArgumentNullException.ThrowIfNull(confirm);

        var options = ParseOptions(args);

        var link = BuildDeleteLink(options);
        DeepLink.TryParse(link, out var parsed);

        var result = new DeleteEntryDeepLinkHandler().Handle(parsed!, currentEntries);

        if (result.RequiresConfirmation && confirm(result))
        {
            result.RecordInto(pendingChanges);
        }

        return result;
    }

    /// <summary>
    /// Turns the recognised options into an equivalent <c>simplehost://delete</c> deep link, including
    /// only the options that were supplied so a missing one is seen as absent by the handler, with the
    /// value percent-encoded so it survives being carried in the link's query string.
    /// </summary>
    private static string BuildDeleteLink(IReadOnlyDictionary<string, string> options)
    {
        var query = new List<string>();
        foreach (var name in new[] { "hostname" })
        {
            if (options.TryGetValue(name, out var value))
            {
                query.Add($"{name}={Uri.EscapeDataString(value)}");
            }
        }

        return $"simplehost://delete?{string.Join('&', query)}";
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
