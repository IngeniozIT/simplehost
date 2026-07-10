using System.Collections.Generic;
using System.Windows;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation;

/// <summary>
/// A modal picker for the destination category of a move. It is built from the current groups: the
/// "No category" group is offered first (as <c>null</c>), then each named category. OK stays disabled
/// until a row is picked. Confirming closes with <see cref="Window.DialogResult"/> set to
/// <see langword="true"/> and the chosen category — <c>null</c> for "No category" — available on
/// <see cref="ChosenCategory"/>.
/// </summary>
public partial class CategoryPickerWindow : Window
{
    private readonly List<string?> _categories = new();

    public CategoryPickerWindow(IEnumerable<CategoryGroupViewModel> groups)
    {
        System.ArgumentNullException.ThrowIfNull(groups);

        InitializeComponent();

        foreach (var group in groups)
        {
            _categories.Add(group.Name);
            CategoryList.Items.Add(group.DisplayName);
        }
    }

    /// <summary>The category the user chose, or <c>null</c> for the "No category" group.</summary>
    public string? ChosenCategory { get; private set; }

    private void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => OkButton.IsEnabled = CategoryList.SelectedIndex >= 0;

    private void OnListDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (CategoryList.SelectedIndex >= 0)
        {
            Confirm();
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => Confirm();

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Confirm()
    {
        ChosenCategory = _categories[CategoryList.SelectedIndex];
        DialogResult = true;
    }
}
