namespace SimpleHost.Presentation;

/// <summary>
/// The reusable host-entry form: the IP address, hostname, comment and category fields with inline
/// IP and hostname validation feedback. It carries no logic of its own — it binds to whatever
/// <see cref="SimpleHost.Presentation.ViewModels.EntryFormViewModel"/> is supplied as its
/// <see cref="System.Windows.FrameworkElement.DataContext"/>, so the add panel binds it to the
/// main view-model's new-entry draft and the edit dialog binds it to the edit draft.
/// </summary>
public partial class EntryFormControl : System.Windows.Controls.UserControl
{
    public EntryFormControl()
    {
        InitializeComponent();
    }
}
