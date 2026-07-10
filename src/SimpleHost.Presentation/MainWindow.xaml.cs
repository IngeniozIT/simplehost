using System;
using System.Windows;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation;

/// <summary>
/// The main SimpleHost window. It binds to the <see cref="MainViewModel"/> assembled by the
/// composition root, rendering the entries read from the resolved <c>hosts</c> file grouped by
/// category, and lets the user manage them by clicking. Entries can be added through the form at the
/// bottom (with the view-model's inline IP and hostname validation), edited through a modal dialog,
/// deleted after a confirmation prompt, enabled or disabled, moved between categories (by the Move
/// action or by dragging a row onto a category), and reordered within a category by dragging. Whole
/// categories can be created, renamed, deleted and enabled or disabled, and a multi-selection of
/// entries can be enabled, disabled, deleted or moved at once. Every action drives the
/// <see cref="MainViewModel"/>'s commands; nothing is written to the file until the pending changes
/// are applied.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private System.Windows.Point _dragStartPoint;
    private HostEntryViewModel? _dragCandidate;

    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        // A delete removes a mapping the user may still rely on, so the view-model stages nothing
        // until the view confirms it. Supply the confirmation as a modal Yes/No prompt naming the
        // entry; declining (or dismissing) leaves the entry in place.
        _viewModel.DeleteConfirmation = ConfirmDelete;
    }

    // --- Adding, editing and deleting a single entry -----------------------------------------

    private void OnAddEntryClick(object sender, RoutedEventArgs e) => _viewModel.SaveNewEntry();

    private void OnEditEntryClick(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is not { } entry)
        {
            return;
        }

        _viewModel.BeginEditEntry(entry);

        var dialog = new EntryEditWindow(_viewModel.EditDraft) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.SaveEditedEntry();
        }
    }

    private void OnDeleteEntryClick(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is { } entry)
        {
            _viewModel.DeleteEntry(entry);
        }
    }

    private bool ConfirmDelete(HostEntryViewModel entry)
    {
        var answer = System.Windows.MessageBox.Show(
            this,
            $"Delete the entry mapping {entry.Hostname} to {entry.IpAddress}?",
            "Delete entry",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return answer == MessageBoxResult.Yes;
    }

    // --- Enabling, disabling and moving a single entry ---------------------------------------

    private void OnToggleEntryStatusClick(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is not { } entry)
        {
            return;
        }

        if (entry.IsActive)
        {
            _viewModel.DisableEntry(entry);
        }
        else
        {
            _viewModel.EnableEntry(entry);
        }
    }

    private void OnMoveEntryClick(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is not { } entry)
        {
            return;
        }

        var picker = new CategoryPickerWindow(_viewModel.Groups) { Owner = this };
        if (picker.ShowDialog() == true)
        {
            _viewModel.MoveEntry(entry, picker.ChosenCategory);
        }
    }

    // --- Multi-selection and its bulk actions ------------------------------------------------

    private void OnEntrySelected(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is { } entry && !_viewModel.SelectedEntries.Contains(entry))
        {
            _viewModel.SelectedEntries.Add(entry);
        }
    }

    private void OnEntryDeselected(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is { } entry)
        {
            _viewModel.SelectedEntries.Remove(entry);
        }
    }

    private void OnEnableSelectedClick(object sender, RoutedEventArgs e)
        => _viewModel.EnableSelectedEntries();

    private void OnDisableSelectedClick(object sender, RoutedEventArgs e)
        => _viewModel.DisableSelectedEntries();

    private void OnDeleteSelectedClick(object sender, RoutedEventArgs e)
    {
        var count = _viewModel.SelectedEntries.Count;
        if (count == 0)
        {
            return;
        }

        var answer = System.Windows.MessageBox.Show(
            this,
            $"Delete the {count} selected {(count == 1 ? "entry" : "entries")}?",
            "Delete entries",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            _viewModel.DeleteSelectedEntries();
        }
    }

    private void OnMoveSelectedClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedEntries.Count == 0)
        {
            return;
        }

        var picker = new CategoryPickerWindow(_viewModel.Groups) { Owner = this };
        if (picker.ShowDialog() == true)
        {
            _viewModel.MoveSelectedEntriesToCategory(picker.ChosenCategory);
        }
    }

    // --- Category management -----------------------------------------------------------------

    private void OnCreateCategoryClick(object sender, RoutedEventArgs e)
    {
        var name = NewCategoryBox.Text.Trim();
        if (name.Length == 0)
        {
            return;
        }

        _viewModel.CreateCategory(name);
        NewCategoryBox.Clear();
    }

    private void OnRenameCategoryClick(object sender, RoutedEventArgs e)
    {
        if (GroupOf(sender) is not { Name: { } currentName } group)
        {
            return;
        }

        var prompt = new TextPromptWindow(
            "Rename category", $"New name for '{group.DisplayName}':", currentName) { Owner = this };
        if (prompt.ShowDialog() == true)
        {
            _viewModel.RenameCategory(currentName, prompt.ResponseText);
        }
    }

    private void OnDeleteCategoryClick(object sender, RoutedEventArgs e)
    {
        if (GroupOf(sender) is not { Name: { } name } group)
        {
            return;
        }

        var answer = System.Windows.MessageBox.Show(
            this,
            $"Delete the category '{group.DisplayName}'? Its entries move to \"No category\".",
            "Delete category",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            _viewModel.DeleteCategory(name);
        }
    }

    private void OnEnableCategoryClick(object sender, RoutedEventArgs e)
    {
        if (GroupOf(sender) is { } group)
        {
            _viewModel.EnableCategory(group.Name);
        }
    }

    private void OnDisableCategoryClick(object sender, RoutedEventArgs e)
    {
        if (GroupOf(sender) is { } group)
        {
            _viewModel.DisableCategory(group.Name);
        }
    }

    // --- Drag and drop: move between categories, reorder within one --------------------------

    private void OnRowMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Ignore presses that land on a button or checkbox — those are clicks, not drags.
        _dragCandidate = IsInteractive(e.OriginalSource as DependencyObject) ? null : EntryOf(sender);
        _dragStartPoint = e.GetPosition(null);
    }

    private void OnRowMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragCandidate is not { } entry || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }

        var moved = e.GetPosition(null) - _dragStartPoint;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragCandidate = null;
        var data = new System.Windows.DataObject(typeof(HostEntryViewModel), entry);
        System.Windows.DragDrop.DoDragDrop((DependencyObject)sender, data, System.Windows.DragDropEffects.Move);
    }

    private void OnEntryDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(HostEntryViewModel))
            ? System.Windows.DragDropEffects.Move
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void OnCategoryDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (DraggedEntry(e) is { } entry && GroupOf(sender) is { } group)
        {
            _viewModel.MoveEntry(entry, group.Name);
        }

        e.Handled = true;
    }

    private void OnRowDrop(object sender, System.Windows.DragEventArgs e)
    {
        e.Handled = true;

        if (DraggedEntry(e) is not { } entry
            || EntryOf(sender) is not { } target
            || ReferenceEquals(entry, target))
        {
            return;
        }

        var targetGroup = GroupContaining(sender as DependencyObject);
        if (targetGroup is null)
        {
            return;
        }

        if (CategoryEquals(CurrentCategoryOf(entry), targetGroup.Name))
        {
            var index = IndexOf(targetGroup, target);
            if (index >= 0)
            {
                _viewModel.ReorderEntry(entry, index);
            }
        }
        else
        {
            // Dropped onto a row of another category: move it there (appended). Reordering happens
            // within a category, so a cross-category drop is treated as a plain move.
            _viewModel.MoveEntry(entry, targetGroup.Name);
        }
    }

    private static HostEntryViewModel? DraggedEntry(System.Windows.DragEventArgs e)
        => e.Data.GetData(typeof(HostEntryViewModel)) as HostEntryViewModel;

    // --- Helpers -----------------------------------------------------------------------------

    private static HostEntryViewModel? EntryOf(object sender)
        => (sender as FrameworkElement)?.DataContext as HostEntryViewModel;

    private static CategoryGroupViewModel? GroupOf(object sender)
        => (sender as FrameworkElement)?.DataContext as CategoryGroupViewModel;

    private string? CurrentCategoryOf(HostEntryViewModel entry)
    {
        foreach (var group in _viewModel.Groups)
        {
            if (group.Entries.Contains(entry))
            {
                return group.Name;
            }
        }

        return null;
    }

    private static int IndexOf(CategoryGroupViewModel group, HostEntryViewModel target)
    {
        for (var index = 0; index < group.Entries.Count; index++)
        {
            if (ReferenceEquals(group.Entries[index], target))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool CategoryEquals(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);

    private static bool IsInteractive(DependencyObject? source)
        => FindAncestor<System.Windows.Controls.Primitives.ButtonBase>(source) is not null;

    private static CategoryGroupViewModel? GroupContaining(DependencyObject? source)
    {
        for (var current = source; current is not null; current = ParentOf(current))
        {
            if (current is FrameworkElement element && element.DataContext is CategoryGroupViewModel group)
            {
                return group;
            }
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = ParentOf(current))
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    private static DependencyObject? ParentOf(DependencyObject current)
        => current is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(current)
            : System.Windows.LogicalTreeHelper.GetParent(current);
}
