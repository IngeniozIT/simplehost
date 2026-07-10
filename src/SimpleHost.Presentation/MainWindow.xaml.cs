using System.Windows;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation;

/// <summary>
/// The main SimpleHost window, shown on demand from the tray's "Open SimpleHost" quick action. It
/// binds to the <see cref="MainViewModel"/> assembled by the composition root, so it renders the
/// entries read from the resolved <c>hosts</c> file. The host-file editing surface is built out by
/// later features; for now the window's data context is the wired view-model.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        DataContext = viewModel;
    }
}
