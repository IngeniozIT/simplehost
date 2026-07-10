using System.Diagnostics.CodeAnalysis;

namespace SimpleHost.Application.DeepLinks;

/// <summary>
/// A parsed <c>simplehost://</c> deep link: the <see cref="DeepLinkAction"/> it asks the app to
/// perform together with the named parameters carried in its query string. Opening such a link
/// launches SimpleHost, and turning the raw link text into this structured form is what lets the app
/// understand it as an action to perform.
/// <para>
/// Because any web page can trigger a link, parsing is total and never trusts its input: an
/// unrecognised scheme, an unknown or missing action, or malformed text is rejected via
/// <see cref="TryParse"/> returning <c>false</c> rather than throwing, so the entry point can surface
/// a clear error instead of crashing. This keeps the WPF deep-link entry point a thin shell that
/// simply hands the raw link to <see cref="TryParse"/>.
/// </para>
/// </summary>
public sealed class DeepLink
{
    /// <summary>The URL scheme every SimpleHost deep link uses: <c>simplehost</c>.</summary>
    public const string Scheme = "simplehost";

    private static readonly IReadOnlyDictionary<string, DeepLinkAction> ActionsByVerb =
        new Dictionary<string, DeepLinkAction>(StringComparer.OrdinalIgnoreCase)
        {
            ["add"] = DeepLinkAction.Add,
            ["update"] = DeepLinkAction.Update,
            ["delete"] = DeepLinkAction.Delete,
            ["enable"] = DeepLinkAction.Enable,
            ["disable"] = DeepLinkAction.Disable,
        };

    private DeepLink(DeepLinkAction action, IReadOnlyDictionary<string, string> parameters)
    {
        Action = action;
        Parameters = parameters;
    }

    /// <summary>The action the link asks the app to perform.</summary>
    public DeepLinkAction Action { get; }

    /// <summary>
    /// The parameters carried in the link's query string, keyed by name. Names are matched
    /// case-insensitively and values are percent-decoded. Empty when the link carries no query.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>
    /// Attempts to parse <paramref name="link"/> as a <c>simplehost://</c> deep link. Returns
    /// <c>true</c> and sets <paramref name="result"/> when the link uses the
    /// <see cref="Scheme"/> scheme and names a known action; returns <c>false</c> and sets
    /// <paramref name="result"/> to <c>null</c> for anything else — a different scheme, an unknown
    /// or missing action, or text that is not an absolute link.
    /// </summary>
    /// <param name="link">The raw link text, as received from the operating system.</param>
    /// <param name="result">The parsed link when parsing succeeds; otherwise <c>null</c>.</param>
    public static bool TryParse(
        [NotNullWhen(true)] string? link, [NotNullWhen(true)] out DeepLink? result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(link))
        {
            return false;
        }

        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!ActionsByVerb.TryGetValue(uri.Host, out var action))
        {
            return false;
        }

        result = new DeepLink(action, ParseParameters(uri.Query));
        return true;
    }

    private static IReadOnlyDictionary<string, string> ParseParameters(string query)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var withoutLeadingQuestionMark = query.TrimStart('?');
        foreach (var segment in withoutLeadingQuestionMark.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string rawName;
            string rawValue;

            var separator = segment.IndexOf('=');
            if (separator < 0)
            {
                rawName = segment;
                rawValue = string.Empty;
            }
            else
            {
                rawName = segment[..separator];
                rawValue = segment[(separator + 1)..];
            }

            var name = Uri.UnescapeDataString(rawName);
            if (name.Length == 0)
            {
                continue;
            }

            parameters[name] = Uri.UnescapeDataString(rawValue);
        }

        return parameters;
    }
}
