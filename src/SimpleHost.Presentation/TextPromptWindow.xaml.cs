using System.Windows;

namespace SimpleHost.Presentation;

/// <summary>
/// A small modal prompt for a single line of text, used to name a category when renaming it. It is
/// opened with a title, a label and the text to start from; OK stays disabled until the box holds
/// non-blank text. Confirming closes the dialog with <see cref="Window.DialogResult"/> set to
/// <see langword="true"/> and the entered text available on <see cref="ResponseText"/>; cancelling
/// returns nothing.
/// </summary>
public partial class TextPromptWindow : Window
{
    public TextPromptWindow(string title, string label, string initialText)
    {
        InitializeComponent();

        Title = title;
        PromptLabel.Text = label;
        ResponseBox.Text = initialText;
        ResponseBox.SelectAll();
        ResponseBox.Focus();
    }

    /// <summary>The text the user entered, trimmed of surrounding whitespace.</summary>
    public string ResponseText => ResponseBox.Text.Trim();

    private void OnResponseChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => OkButton.IsEnabled = !string.IsNullOrWhiteSpace(ResponseBox.Text);

    private void OnOkClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
