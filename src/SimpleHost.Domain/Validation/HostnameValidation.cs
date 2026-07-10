namespace SimpleHost.Domain.Validation;

/// <summary>
/// The outcome of checking whether a candidate string is a well-formed hostname, produced as the
/// user types so the UI can indicate validity inline and gate saving the entry on it.
/// </summary>
/// <remarks>
/// A candidate is well-formed when it is a dot-separated sequence of labels following the
/// preferred-name rules of RFC&#160;1123: the whole name is at most 253 characters; each label is
/// 1-63 characters of ASCII letters, digits or hyphens; and no label starts or ends with a hyphen.
/// Empty labels (a leading, trailing or doubled dot), underscores, whitespace and any other
/// punctuation are rejected, because a <c>hosts</c> file entry needs an unambiguous hostname.
/// Matching is case-insensitive.
/// </remarks>
public sealed record HostnameValidation
{
    private const string EmptyPrompt = "Enter a hostname.";
    private const int MaxHostnameLength = 253;
    private const int MaxLabelLength = 63;

    private HostnameValidation(bool isWellFormed, string message)
    {
        IsWellFormed = isWellFormed;
        Message = message;
    }

    /// <summary>
    /// Whether the candidate is a well-formed hostname and the entry may be saved.
    /// </summary>
    public bool IsWellFormed { get; }

    /// <summary>
    /// A human-readable message for inline display: empty when the candidate is well-formed, a
    /// prompt when nothing has been entered yet, and an explanation naming the offending input
    /// when the candidate is not a valid hostname.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Validates <paramref name="candidate"/> as it is typed.
    /// </summary>
    /// <param name="candidate">The text currently entered in the hostname field.</param>
    /// <returns>
    /// A well-formed result with an empty message for a valid hostname; a not-well-formed result
    /// prompting for input when <paramref name="candidate"/> is <c>null</c>, empty or only
    /// whitespace; otherwise a not-well-formed result whose message names the invalid candidate.
    /// </returns>
    public static HostnameValidation Of(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return new HostnameValidation(false, EmptyPrompt);
        }

        if (IsWellFormedHostname(candidate))
        {
            return new HostnameValidation(true, string.Empty);
        }

        return new HostnameValidation(false, $"'{candidate}' is not a valid hostname.");
    }

    private static bool IsWellFormedHostname(string candidate)
    {
        if (candidate.Length > MaxHostnameLength)
        {
            return false;
        }

        var labels = candidate.Split('.');
        foreach (var label in labels)
        {
            if (!IsWellFormedLabel(label))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWellFormedLabel(string label)
    {
        if (label.Length is < 1 or > MaxLabelLength)
        {
            return false;
        }

        if (label[0] == '-' || label[^1] == '-')
        {
            return false;
        }

        foreach (var character in label)
        {
            if (!IsHostnameCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsHostnameCharacter(char character) =>
        character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-';
}
