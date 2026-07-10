using System;
using System.Windows;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation;

/// <summary>
/// The main SimpleHost window. It binds to the <see cref="MainViewModel"/> assembled by the
/// composition root, rendering the entries read from the resolved <c>hosts</c> file grouped by
/// category. On top of showing them it lets the user manage entries by clicking: adding a new entry
/// through the form at the bottom (with the view-model's inline IP and hostname validation), editing
/// an existing entry's IP, hostname, comment and category through a modal dialog, and deleting an
/// entry after a confirmation prompt. Each action drives the <see cref="MainViewModel"/>'s
/// pending-change commands; nothing is written to the file until the pending changes are applied.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

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

    private static HostEntryViewModel? EntryOf(object sender)
        => (sender as FrameworkElement)?.DataContext as HostEntryViewModel;
}
