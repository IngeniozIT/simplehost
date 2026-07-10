using System.Windows;

namespace SimpleHost.Presentation;

/// <summary>
/// The main SimpleHost window, shown on demand from the tray's "Open SimpleHost" quick action. It is
/// a thin shell for now; the host-file editing surface is built out by later features.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
