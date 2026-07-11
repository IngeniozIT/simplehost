using System.Windows;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation;

/// <summary>
/// The modal Profiles dialog, opened from the Profiles menu. It binds to the shared
/// <see cref="MainViewModel"/>, so switching to a saved profile or saving the current setup drives the
/// same view-model methods — staging the same pending changes — as the former Profiles tab did.
/// </summary>
public partial class ProfilesWindow : Window
{
    private readonly MainViewModel _viewModel;

    public ProfilesWindow(MainViewModel viewModel)
    {
        System.ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
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

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
