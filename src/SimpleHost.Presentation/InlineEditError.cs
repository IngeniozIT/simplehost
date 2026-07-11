using System.Windows;

namespace SimpleHost.Presentation;

/// <summary>
/// Attached state used to flag an inline, in-row entry field whose last committed value failed
/// validation: <see cref="MessageProperty"/> carries the reason to show directly beneath the field
/// and <see cref="HasErrorProperty"/> drives the field's red error outline. Both are cleared once the
/// value becomes valid again or the edit is abandoned, so a calm row shows no error.
/// </summary>
public static class InlineEditError
{
    /// <summary>
    /// The validation-failure reason to display beneath the field, or an empty string when the field
    /// has no error.
    /// </summary>
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.RegisterAttached(
            "Message",
            typeof(string),
            typeof(InlineEditError),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// Whether the field currently has a validation error, used to turn its border red.
    /// </summary>
    public static readonly DependencyProperty HasErrorProperty =
        DependencyProperty.RegisterAttached(
            "HasError",
            typeof(bool),
            typeof(InlineEditError),
            new PropertyMetadata(false));

    /// <summary>Sets the inline validation message shown beneath <paramref name="element"/>.</summary>
    public static void SetMessage(DependencyObject element, string value) =>
        element.SetValue(MessageProperty, value);

    /// <summary>Gets the inline validation message shown beneath <paramref name="element"/>.</summary>
    public static string GetMessage(DependencyObject element) =>
        (string)element.GetValue(MessageProperty);

    /// <summary>Sets whether <paramref name="element"/> currently has a validation error.</summary>
    public static void SetHasError(DependencyObject element, bool value) =>
        element.SetValue(HasErrorProperty, value);

    /// <summary>Gets whether <paramref name="element"/> currently has a validation error.</summary>
    public static bool GetHasError(DependencyObject element) =>
        (bool)element.GetValue(HasErrorProperty);
}
