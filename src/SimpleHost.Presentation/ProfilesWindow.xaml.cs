using System.Windows;
using SimpleHost.Domain.Profiles;
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

    private void OnPreviewProfileClick(object sender, RoutedEventArgs e)
    {
        // Show what recalling the selected profile would apply (a category preset) or restore (a
        // snapshot such as "Default"), derived from the view-model. With nothing selected, do nothing.
        if (ProfilesBox.SelectedValue is not string name || _viewModel.PreviewProfile(name) is not { } preview)
        {
            return;
        }

        System.Windows.MessageBox.Show(
            this, DescribePreview(preview), $"Preview profile '{preview.Name}'",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnRenameProfileClick(object sender, RoutedEventArgs e)
    {
        // Rename the selected profile through the view-model, which persists the change and keeps the
        // renamed profile activatable. A blank name is ignored by the view-model, and the prompt keeps
        // its OK button disabled until non-blank text is entered.
        if (ProfilesBox.SelectedValue is not string name)
        {
            return;
        }

        var prompt = new TextPromptWindow("Rename profile", $"New name for '{name}':", name) { Owner = this };
        if (prompt.ShowDialog() == true)
        {
            _viewModel.RenameProfile(name, prompt.ResponseText);
        }
    }

    private void OnRemoveProfileClick(object sender, RoutedEventArgs e)
    {
        // Remove the selected profile from the list and persistence after confirming. The revert
        // snapshot (the built-in "Default") is protected: the view-model refuses to remove it, and we
        // tell the user why rather than showing a delete confirmation that would do nothing.
        if (ProfilesBox.SelectedItem is not Profile profile)
        {
            return;
        }

        if (profile.RestoresSnapshot)
        {
            System.Windows.MessageBox.Show(
                this,
                $"'{profile.Name}' is the revert-to-original snapshot and cannot be removed, so the "
                    + "one-click way back to the original hosts file is always available.",
                "Remove profile",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var answer = System.Windows.MessageBox.Show(
            this,
            $"Remove the profile '{profile.Name}'? This cannot be undone.",
            "Remove profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            _viewModel.RemoveProfile(profile.Name);
        }
    }

    private static string DescribePreview(ProfilePreview preview)
    {
        var categories = preview.Categories.Count == 0
            ? "(none)"
            : string.Join(", ", preview.Categories);

        return preview.RestoresSnapshot
            ? $"Switching to '{preview.Name}' restores the whole original hosts file captured when the "
                + $"profile was saved.\n\nCategories in the restored file: {categories}."
            : $"Switching to '{preview.Name}' enables these categories and disables every other one:"
                + $"\n\n{categories}.";
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
