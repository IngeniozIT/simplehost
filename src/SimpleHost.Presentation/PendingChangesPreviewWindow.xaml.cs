using System;
using System.Windows;
using SimpleHost.Domain.Preview;

namespace SimpleHost.Presentation;

/// <summary>
/// A modal, read-only preview of the pending changes, opened from the main window's Preview action.
/// It renders the <see cref="HostsFileDiff"/> the view-model computed as a diff — added lines marked
/// "+", removed lines marked "−", unchanged lines kept dimmed for context — so the user can see
/// exactly what applying will write before committing to it. When the pending changes make no
/// difference to the file, it says so instead of showing an empty diff. It writes nothing; Close
/// simply dismisses it.
/// </summary>
public partial class PendingChangesPreviewWindow : Window
{
    public PendingChangesPreviewWindow(HostsFileDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);

        InitializeComponent();

        if (diff.HasChanges)
        {
            DiffLines.ItemsSource = diff.Lines;
        }
        else
        {
            DiffPanel.Visibility = Visibility.Collapsed;
            NoChangesLabel.Visibility = Visibility.Visible;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
