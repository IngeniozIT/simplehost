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
/// bottom (with the view-model's inline IP and hostname validation), edited in place by clicking a
/// row's IP address, hostname or description and typing (or through a modal dialog), deleted after a
/// confirmation prompt, enabled or disabled, moved between categories (by the Move
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

    // Guards the inline-edit commit against re-entering itself: staging a real edit rebuilds the list
    // and tears down the very row (and text box) being committed, which fires LostKeyboardFocus again.
    private bool _suppressInlineCommit;

    // Same re-entrancy guard for the inline "add entry" draft row: committing it stages an add, which
    // rebuilds the list and disposes the draft row, firing its LostKeyboardFocus again mid-commit.
    private bool _suppressAddCommit;

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

        // The Profiles menu lists one item per saved profile beneath its static Save entry. Keep it in
        // step with the view-model's profile set — populated by LoadPersistedProfiles and grown by
        // SaveProfile — by rebuilding whenever that collection changes, and once now for the initial set.
        _viewModel.Profiles.CollectionChanged += OnProfilesChanged;
        RebuildProfilesMenu();
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

    private void OnAddEntryInlineClick(object sender, RoutedEventArgs e)
    {
        // The plus button in a category header opens an inline, editable draft row at the bottom of
        // that category. The draft is bound to the view-model's new-entry draft; the user types the
        // fields directly into the row and commits them there (see the OnAddRow* handlers), so no
        // separate window is opened.
        if (GroupOf(sender) is { } group)
        {
            _viewModel.BeginAddEntry(group.Name);
        }
    }

    // --- Inline "add entry" draft row: type the fields directly and commit in place --------------

    private void OnAddRowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Enter commits the drafted entry; Escape abandons it. Both consume the key so it does not also
        // ring the default-button bell or bubble up as list navigation.
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            CommitAddRow();
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            e.Handled = true;
            _viewModel.CancelAddEntry();
        }
    }

    private void OnAddRowLostFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        // Leaving the whole draft row (a click or Tab out of it) commits it, exactly like pressing
        // Enter. Focus moving between the row's own fields and buttons is not a leave, so ignore it —
        // otherwise tabbing from the IP field to the hostname field would commit prematurely.
        if (sender is System.Windows.Controls.Border row
            && e.NewFocus is DependencyObject focus
            && IsWithin(focus, row))
        {
            return;
        }

        CommitAddRow();
    }

    private void OnAddRowConfirmClick(object sender, RoutedEventArgs e) => CommitAddRow();

    private void OnAddRowCancelClick(object sender, RoutedEventArgs e) => _viewModel.CancelAddEntry();

    private void OnAddRowVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // When the draft row appears, put the caret in the IP field so the user can type immediately.
        // Defer to Input priority so the row is laid out (and focusable) before we focus it.
        if (e.NewValue is true && sender is System.Windows.Controls.Border row)
        {
            row.Dispatcher.BeginInvoke(
                new Action(() => FindDescendant<Wpf.Ui.Controls.TextBox>(row)?.Focus()),
                System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void CommitAddRow()
    {
        // Staging the add rebuilds the list and disposes this draft row synchronously — which re-enters
        // here through the row's own LostKeyboardFocus — so hold the guard across the commit. An empty
        // draft is discarded and a valid one staged; an invalid, partially-typed draft is left open so
        // the user can fix it (CommitNewEntry returns false and changes nothing).
        if (_suppressAddCommit)
        {
            return;
        }

        _suppressAddCommit = true;
        try
        {
            _viewModel.CommitNewEntry();
        }
        finally
        {
            _suppressAddCommit = false;
        }
    }

    private void OnDeleteEntryClick(object sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is { } entry)
        {
            _viewModel.DeleteEntry(entry);
        }
    }

    // --- Inline editing of a single entry field, directly in its row -------------------------

    private void OnInlineEditKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox box)
        {
            return;
        }

        // Enter commits the typed value; Escape abandons it and restores what was there. Both consume
        // the key so it does not also ring the default-button bell or bubble up as a list navigation.
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            CommitInlineEdit(box);
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            e.Handled = true;
            RevertInlineEdit(box);
        }
    }

    private void OnInlineEditLostFocus(object sender, RoutedEventArgs e)
    {
        // Leaving the field (Tab, a click elsewhere) commits it, exactly like pressing Enter.
        if (sender is System.Windows.Controls.TextBox box)
        {
            CommitInlineEdit(box);
        }
    }

    private void CommitInlineEdit(System.Windows.Controls.TextBox box)
    {
        if (_suppressInlineCommit || EntryOf(box) is not { } entry)
        {
            return;
        }

        // Staging an accepted change rebuilds the list and disposes this row synchronously — which
        // re-enters here through the box's own LostKeyboardFocus — so hold the guard across the call.
        _suppressInlineCommit = true;
        try
        {
            var result = _viewModel.EditEntryField(entry, FieldOf(box), box.Text);
            if (result.IsAccepted)
            {
                ClearInlineError(box);
            }
            else
            {
                // Rejected as invalid: keep the offending text visible and flag the field in place with
                // a red outline and the reason beneath it, instead of silently restoring the old value.
                ShowInlineError(box, result.ErrorMessage);
            }
        }
        finally
        {
            _suppressInlineCommit = false;
        }
    }

    private void OnInlineEditTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // While a rejected field is flagged, re-check the value as it is typed and clear the error the
        // moment it becomes valid again, so the red outline and message disappear as soon as it is fixed.
        if (sender is System.Windows.Controls.TextBox box
            && InlineEditError.GetHasError(box)
            && MainViewModel.ValidateFieldValue(FieldOf(box), box.Text).Length == 0)
        {
            ClearInlineError(box);
        }
    }

    private static void RevertInlineEdit(System.Windows.Controls.TextBox box)
    {
        // Abandoning the edit (Escape) clears any error flag and re-pulls the entry's current value from
        // the (one-way) binding source rather than assigning Text directly, which would clear the
        // binding. This restores the last committed value and a calm, unflagged field.
        ClearInlineError(box);
        box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateTarget();
    }

    private static void ShowInlineError(System.Windows.Controls.TextBox box, string message)
    {
        InlineEditError.SetMessage(box, message);
        InlineEditError.SetHasError(box, true);
    }

    private static void ClearInlineError(System.Windows.Controls.TextBox box)
    {
        InlineEditError.SetMessage(box, string.Empty);
        InlineEditError.SetHasError(box, false);
    }

    private static HostEntryField FieldOf(System.Windows.Controls.TextBox box)
        => box.Tag switch
        {
            "IpAddress" => HostEntryField.IpAddress,
            "Hostname" => HostEntryField.Hostname,
            _ => HostEntryField.Comment,
        };

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
        // The inline "new category" affordance in the Entries list: staging a create rebuilds the group
        // list so the new, empty category appears above, then the input clears and keeps focus so the
        // user can type the next one. A blank or whitespace-only name creates nothing.
        var name = NewCategoryBox.Text.Trim();
        if (name.Length == 0)
        {
            return;
        }

        _viewModel.CreateCategory(name);
        NewCategoryBox.Clear();
        NewCategoryBox.Focus();
    }

    private void OnNewCategoryKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Enter creates the category, just like clicking the plus; consume the key so it does not also
        // ring the default-button bell.
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            OnCreateCategoryClick(sender, e);
        }
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

    // --- Menu bar: Profiles, Subscriptions and the About box ---------------------------------

    private void OnOpenProfilesClick(object sender, RoutedEventArgs e)
        // The Profiles menu opens a modal dialog bound to the same view-model, so saving the current
        // setup or switching to a saved profile stages exactly the pending changes it did as a tab.
        => new ProfilesWindow(_viewModel) { Owner = this }.ShowDialog();

    // Marks the profile items this window generates into the Profiles menu, so a rebuild can remove
    // exactly those (leaving the static Save entry and separator authored in XAML untouched).
    private static readonly object ProfileMenuItemMarker = new();

    private void OnProfilesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => RebuildProfilesMenu();

    private void RebuildProfilesMenu()
    {
        // Drop the previously generated items (marked ones) and regenerate from the current profile set,
        // so the menu always reflects MainViewModel.Profiles. The static Save entry and the separator
        // above the generated list are authored in XAML and left in place.
        for (var index = ProfilesMenu.Items.Count - 1; index >= 0; index--)
        {
            if (ProfilesMenu.Items[index] is FrameworkElement { Tag: { } tag }
                && ReferenceEquals(tag, ProfileMenuItemMarker))
            {
                ProfilesMenu.Items.RemoveAt(index);
            }
        }

        if (_viewModel.Profiles.Count == 0)
        {
            // No profile saved yet: show a disabled placeholder rather than leaving the section empty.
            ProfilesMenu.Items.Add(new System.Windows.Controls.MenuItem
            {
                Header = "No saved profiles",
                IsEnabled = false,
                Tag = ProfileMenuItemMarker,
            });
            return;
        }

        foreach (var profile in _viewModel.Profiles)
        {
            // Capture the name so each item activates its own profile through the normal pending-changes
            // flow (ActivateProfile), staging exactly what switching to it in the dialog would.
            var name = profile.Name;
            var item = new System.Windows.Controls.MenuItem
            {
                Header = $"Use profile {name}",
                Tag = ProfileMenuItemMarker,
            };
            item.Click += (_, _) => _viewModel.ActivateProfile(name);
            ProfilesMenu.Items.Add(item);
        }
    }

    private void OnOpenSubscriptionsClick(object sender, RoutedEventArgs e)
        // The Subscriptions menu opens a modal dialog bound to the same view-model, so subscribing to
        // and refreshing remote lists behaves exactly as it did on the former tab.
        => new SubscriptionsWindow(_viewModel) { Owner = this }.ShowDialog();

    private void OnAboutClick(object sender, RoutedEventArgs e)
        => System.Windows.MessageBox.Show(
            this,
            "SimpleHost\n\nManage the Windows hosts file, organised by category.",
            "About SimpleHost",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    // --- Import and export external hosts files ----------------------------------------------

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        // Import reads a chosen hosts file straight into the "No category" (uncategorised) set — no
        // category is selected, so the user only picks the source file.
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Import hosts file" };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.ImportEntries(dialog.FileName, null);
        }
    }

    private void OnExportConfigurationClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export hosts", FileName = "hosts" };
        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.ExportConfiguration(dialog.FileName);
        }
    }

    private void ReportOperationFailure(string message)
        // A subscription/import/export failure is reported over whichever window is active — the
        // Subscriptions dialog when it drove the operation, otherwise this main window — so the warning
        // is never hidden behind a modal child.
        => System.Windows.MessageBox.Show(
            ActiveWindow(), message, "SimpleHost", MessageBoxButton.OK, MessageBoxImage.Warning);

    private Window ActiveWindow()
    {
        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            if (window.IsActive)
            {
                return window;
            }
        }

        return this;
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
        => FindAncestor<System.Windows.Controls.Primitives.ButtonBase>(source) is not null
        || FindAncestor<System.Windows.Controls.Primitives.TextBoxBase>(source) is not null;

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

    private static bool IsWithin(DependencyObject? node, DependencyObject ancestor)
    {
        for (var current = node; current is not null; current = ParentOf(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static DependencyObject? ParentOf(DependencyObject current)
        => current is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(current)
            : System.Windows.LogicalTreeHelper.GetParent(current);
}
