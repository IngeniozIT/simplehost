using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using SimpleHost.Presentation.ViewModels;
using SimpleHost.Presentation.ViewModels.DeepLinks;

namespace SimpleHost.Presentation;

/// <summary>
/// The main SimpleHost window. It binds to the <see cref="MainViewModel"/> assembled by the
/// composition root, rendering the entries read from the resolved <c>hosts</c> file grouped by
/// category, and lets the user manage them by clicking. Entries can be added through the form at the
/// bottom (with the view-model's inline IP and hostname validation), edited through a modal dialog,
/// deleted after a confirmation prompt, enabled or disabled, moved between categories (by the Move
/// action or by dragging a row onto a category), and reordered within a category by dragging. Whole
/// categories can be created, renamed, deleted and enabled or disabled, and a multi-selection of
/// entries can be enabled, disabled, deleted or moved at once. The current set of enabled categories
/// can be saved as a named profile and a saved profile switched back to. Remote subscriptions can be
/// added and refreshed, and external hosts files imported into a category or exported (a single
/// category or the whole configuration). A settings area exposes the flush-DNS-after-saving toggle.
/// Every action drives the <see cref="MainViewModel"/>'s commands; nothing is written to the file
/// until the pending changes are applied.
/// </summary>
public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    // How tall the animated insertion gap grows, and how quickly it opens and closes. The gap opens
    // above the row the cursor is over, which is exactly where a dropped entry lands (see OnRowDrop).
    private static readonly TimeSpan GapOpenDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan GapCloseDuration = TimeSpan.FromMilliseconds(120);

    private readonly MainViewModel _viewModel;
    private System.Windows.Point _dragStartPoint;
    private HostEntryViewModel? _dragCandidate;

    // Live state for the in-flight drag: the floating ghost, its layer, the dimmed source row, where
    // inside that row the user grabbed, and the row currently showing an opened insertion gap.
    private DragAdorner? _dragAdorner;
    private AdornerLayer? _adornerLayer;
    private System.Windows.UIElement? _draggedRow;
    private System.Windows.Vector _grabOffset;
    private System.Windows.Controls.Border? _gapRow;

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

        // A simplehost:// update, delete or disable link can be triggered by any web page, so the
        // view-model stages nothing for those actions until the view confirms. Supply the
        // confirmation as a modal Yes/No prompt describing the change; declining stages nothing.
        _viewModel.DeepLinkConfirmation = ConfirmDeepLink;

        // A refused subscription, import or export changes nothing and reports its reason through these
        // callbacks; surface each as a modal warning so the user learns why nothing happened.
        _viewModel.SubscriptionOperationFailed = ReportOperationFailure;
        _viewModel.ImportExportFailed = ReportOperationFailure;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // SimpleHost stays running in the tray, so closing the window hides it rather than destroying
        // it — the same window (and its theme watcher) is re-shown from the tray's "Open" action. The
        // app quits only through the tray's Exit action.
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    // --- Search and category filter ----------------------------------------------------------

    private void OnCategoryFilterChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Picking a category (including the "No category" group, whose Name is null) narrows to it;
        // clearing the selection turns the category filter off. The hostname and IP boxes drive the
        // view-model's search queries directly through their bindings.
        if (CategoryFilterBox.SelectedItem is CategoryGroupViewModel group)
        {
            _viewModel.FilterByCategory(group.Name);
        }
        else
        {
            _viewModel.ClearCategoryFilter();
        }
    }

    private void OnClearFiltersClick(object sender, RoutedEventArgs e)
    {
        // Clearing the box resets the search query through its binding; dropping the combo's
        // selection turns the category filter off via OnCategoryFilterChanged.
        SearchBox.Clear();
        CategoryFilterBox.SelectedItem = null;
    }

    // --- Pending changes: preview, apply, discard --------------------------------------------

    private void OnPreviewPendingChangesClick(object sender, RoutedEventArgs e)
    {
        // Drive the view-model to compute the diff of what applying would write, then show it in a
        // read-only modal. Apply and Discard are bound straight to the view-model's commands in XAML.
        _viewModel.PreviewCommand.Execute(null);

        if (_viewModel.PreviewDiff is not { } diff)
        {
            return;
        }

        new PendingChangesPreviewWindow(diff) { Owner = this }.ShowDialog();
    }

    // --- Adding, editing and deleting a single entry -----------------------------------------

    private void OnAddEntryClick(object sender, RoutedEventArgs e)
    {
        // Adding reuses the same modal form as editing, bound to the view-model's new-entry draft.
        // Reset it so the form opens empty, and stage the entry only when the user confirms a valid one.
        _viewModel.NewEntry.Reset();

        var dialog = new EntryEditWindow(_viewModel.NewEntry) { Owner = this, Title = "Add entry" };
        if (dialog.ShowDialog() == true)
        {
            _viewModel.SaveNewEntry();
        }
    }

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

    private bool ConfirmDeepLink(DeepLinkConfirmationRequest request)
    {
        var answer = System.Windows.MessageBox.Show(
            this,
            request.Description,
            "SimpleHost",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        return answer == MessageBoxResult.Yes;
    }

    // --- Enabling, disabling and moving a single entry ---------------------------------------

    private void OnRowToggle(object sender, RoutedEventArgs e)
    {
        // The row's toggle switch is bound one-way to IsActive, so this fires only on a genuine user
        // flip (never when the binding paints the current state). Stage the opposite of the entry's
        // current state; the resulting refresh rebuilds the row so the switch reflects the new value.
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

    // --- Profiles: save the current enabled categories, switch to a saved one ----------------

    private void OnSaveProfileClick(object sender, RoutedEventArgs e)
    {
        // Save the categories that are enabled right now under the typed name, replacing any profile
        // already saved under it. A blank name saves nothing, so the view-model ignores it; clear the
        // box either way once the save has been driven.
        var name = ProfileNameBox.Text.Trim();
        if (name.Length == 0)
        {
            return;
        }

        _viewModel.SaveProfile(name);
        ProfileNameBox.Clear();
    }

    private void OnSwitchProfileClick(object sender, RoutedEventArgs e)
    {
        // Recall the profile picked in the combo by its name (SelectedValuePath), staging the pending
        // changes that enable its categories and disable the rest. With nothing selected, do nothing.
        if (ProfilesBox.SelectedValue is string name)
        {
            _viewModel.ActivateProfile(name);
        }
    }

    // --- Remote subscriptions: subscribe, refresh one, refresh all ---------------------------

    private void OnSubscribeClick(object sender, RoutedEventArgs e)
        => _viewModel.SubscribeToRemoteList(SubscriptionUrlBox.Text.Trim(), SubscriptionCategoryBox.Text.Trim());

    private void OnRefreshSubscriptionClick(object sender, RoutedEventArgs e)
        => _viewModel.RefreshSubscription(SubscriptionUrlBox.Text.Trim(), SubscriptionCategoryBox.Text.Trim());

    private void OnRefreshAllSubscriptionsClick(object sender, RoutedEventArgs e)
        => _viewModel.RefreshAllSubscriptions();

    // --- Import and export external hosts files ----------------------------------------------

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        // Import needs a target category picked (its Name is null for the "No category" group).
        if (TransferCategoryBox.SelectedItem is not CategoryGroupViewModel group)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Import hosts file" };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.ImportEntries(dialog.FileName, group.Name);
        }
    }

    private void OnExportCategoryClick(object sender, RoutedEventArgs e)
    {
        // Export-category needs the source category picked (null Name is the "No category" group).
        if (TransferCategoryBox.SelectedItem is not CategoryGroupViewModel group)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export category", FileName = "hosts" };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.ExportCategory(group.Name, dialog.FileName);
        }
    }

    private void OnExportConfigurationClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export configuration", FileName = "hosts" };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.ExportConfiguration(dialog.FileName);
        }
    }

    private void ReportOperationFailure(string message)
        => System.Windows.MessageBox.Show(
            this, message, "SimpleHost", MessageBoxButton.OK, MessageBoxImage.Warning);

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
        var row = (System.Windows.UIElement)sender;

        // Show the floating ghost and dim the source row for the duration of the drag. DoDragDrop is
        // blocking, so the drag visuals are always torn down in the finally, whether the user drops or
        // presses Escape. The drop itself (OnRowDrop / OnCategoryDrop) is left exactly as it was.
        BeginDragVisual(row, e.GetPosition(row));
        try
        {
            var data = new System.Windows.DataObject(typeof(HostEntryViewModel), entry);
            System.Windows.DragDrop.DoDragDrop((DependencyObject)sender, data, System.Windows.DragDropEffects.Move);
        }
        finally
        {
            EndDragVisual();
        }
    }

    private void OnEntryDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(HostEntryViewModel))
            ? System.Windows.DragDropEffects.Move
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    // --- Drag visuals: the floating ghost and the animated insertion gap ---------------------

    private void OnEntriesPreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        // Fires continuously as the cursor moves anywhere over the list while a drag is in flight.
        // Keep the ghost under the cursor and open a gap on the row it would drop onto.
        if (_dragAdorner is null)
        {
            return;
        }

        MoveGhost(e.GetPosition(RootGrid));
        UpdateInsertionGap(e.OriginalSource as DependencyObject);
    }

    private void BeginDragVisual(System.Windows.UIElement row, System.Windows.Point grabPoint)
    {
        _adornerLayer = AdornerLayer.GetAdornerLayer(RootGrid);
        if (_adornerLayer is null || row is not FrameworkElement rowElement)
        {
            // No adorner layer (unexpected): fall back to a plain drag with no ghost.
            return;
        }

        _draggedRow = row;
        _grabOffset = new System.Windows.Vector(grabPoint.X, grabPoint.Y);
        _dragAdorner = new DragAdorner(RootGrid, rowElement);
        _adornerLayer.Add(_dragAdorner);

        // Dim the original so the ghost reads as the row lifted out of the list.
        row.Opacity = 0.35;
    }

    private void MoveGhost(System.Windows.Point cursorInRoot)
        => _dragAdorner?.SetOffset(new System.Windows.Point(
            cursorInRoot.X - _grabOffset.X, cursorInRoot.Y - _grabOffset.Y));

    private void UpdateInsertionGap(DependencyObject? overSource)
    {
        var row = RowBorderOf(overSource);

        if (row is null)
        {
            // Over a category header, the opened gap itself, or empty space. Leave the current gap in
            // place rather than fighting the layout shift the gap causes: opening a gap above a row
            // pushes that row down, so the cursor can land in the gap (which belongs to no row) — and
            // closing on that would flicker. The gap is always closed when the drag ends (EndDragVisual)
            // and is replaced the moment the cursor reaches another row.
            return;
        }

        if (ReferenceEquals(row, _draggedRow))
        {
            // Hovering the dragged entry's own slot: no meaningful move, so show no gap.
            CloseGap();
            return;
        }

        OpenGap(row);
    }

    private void OpenGap(System.Windows.Controls.Border row)
    {
        if (ReferenceEquals(_gapRow, row))
        {
            return;
        }

        CloseGap();
        _gapRow = row;

        // Grow the row's top margin so a gap opens above it — the slot the entry would drop into.
        var gapHeight = _draggedRow is FrameworkElement dragged && dragged.RenderSize.Height > 0
            ? dragged.RenderSize.Height
            : 34.0;
        var animation = new ThicknessAnimation(new Thickness(0, 1 + gapHeight, 0, 1), GapOpenDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        row.BeginAnimation(FrameworkElement.MarginProperty, animation);
    }

    private void CloseGap()
    {
        if (_gapRow is not { } row)
        {
            return;
        }

        _gapRow = null;

        // Animate the margin back to its resting value, then release the animation so the XAML value
        // (0,1) governs the row again.
        var animation = new ThicknessAnimation(new Thickness(0, 1, 0, 1), GapCloseDuration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        animation.Completed += (_, _) => row.BeginAnimation(FrameworkElement.MarginProperty, null);
        row.BeginAnimation(FrameworkElement.MarginProperty, animation);
    }

    private void EndDragVisual()
    {
        CloseGap();

        if (_draggedRow is not null)
        {
            // Clear the local opacity so the disabled-row dimming trigger (0.55) can take over again.
            _draggedRow.ClearValue(System.Windows.UIElement.OpacityProperty);
            _draggedRow = null;
        }

        if (_adornerLayer is not null && _dragAdorner is not null)
        {
            _adornerLayer.Remove(_dragAdorner);
        }

        _dragAdorner = null;
        _adornerLayer = null;
    }

    private static System.Windows.Controls.Border? RowBorderOf(DependencyObject? source)
    {
        for (var current = source; current is not null; current = ParentOf(current))
        {
            if (current is System.Windows.Controls.Border { Name: "Row" } row)
            {
                return row;
            }
        }

        return null;
    }

    private void OnEntriesDrop(object sender, System.Windows.DragEventArgs e)
    {
        // A drop that lands in the opened gap (which belongs to no row) or other empty list space
        // still has a clear intended target: the row the gap was opened above. Honor it so dropping
        // into the visible gap reorders/moves exactly as dropping on that row would — the same
        // ReorderEntry / MoveEntry staging as OnRowDrop. Drops that hit a row or a category header are
        // already handled (and marked Handled) by OnRowDrop / OnCategoryDrop and never reach here.
        e.Handled = true;

        if (_gapRow is not { } gapRow
            || DraggedEntry(e) is not { } entry
            || gapRow.DataContext is not HostEntryViewModel target
            || ReferenceEquals(entry, target))
        {
            return;
        }

        var targetGroup = GroupContaining(gapRow);
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
            _viewModel.MoveEntry(entry, targetGroup.Name);
        }
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
