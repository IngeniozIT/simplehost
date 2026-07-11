namespace SimpleHost.Presentation.ViewModels;

/// <summary>
/// The outcome of committing a single inline, in-row edit of a host entry field through
/// <see cref="MainViewModel.EditEntryField"/>: whether the new value was accepted (staged, or an
/// unchanged no-op) and, when it was rejected, the human-readable reason the value is invalid, so
/// the view can flag the offending field in place instead of silently reverting it.
/// </summary>
public sealed record InlineFieldEditResult
{
    private InlineFieldEditResult(bool isAccepted, string errorMessage)
    {
        IsAccepted = isAccepted;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// A shared accepted result: the value was valid and has been staged (or left the field
    /// unchanged), carrying no error message.
    /// </summary>
    public static InlineFieldEditResult Accepted { get; } = new(true, string.Empty);

    /// <summary>
    /// A rejected result carrying the reason the value is invalid, for display beneath the field.
    /// </summary>
    /// <param name="errorMessage">The validation-failure reason to show; must be non-empty.</param>
    public static InlineFieldEditResult Rejected(string errorMessage) => new(false, errorMessage);

    /// <summary>
    /// Whether the edited value was accepted. <c>true</c> when it was valid (whether it staged a
    /// change or was an unchanged no-op); <c>false</c> when it was rejected as invalid.
    /// </summary>
    public bool IsAccepted { get; }

    /// <summary>
    /// The reason the value was rejected, for inline display: empty when the value was accepted,
    /// otherwise an explanation naming the offending input.
    /// </summary>
    public string ErrorMessage { get; }
}
