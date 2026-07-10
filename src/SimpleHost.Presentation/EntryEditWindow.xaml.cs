using System;
using System.Windows;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation;

/// <summary>
/// The modal dialog for editing one existing entry's IP address, hostname, comment and category. It
/// binds to the main view-model's <see cref="MainViewModel.EditDraft"/> — already populated by
/// <see cref="MainViewModel.BeginEditEntry"/> — so the fields show the entry's current values with
/// the same inline validation as the add form. Confirming closes the dialog with
/// <see cref="Window.DialogResult"/> set to <see langword="true"/>; the caller stages the edit
/// through <see cref="MainViewModel.SaveEditedEntry"/>. Cancelling leaves the draft untouched and
/// stages nothing.
/// </summary>
public partial class EntryEditWindow : Window
{
    public EntryEditWindow(EntryFormViewModel editDraft)
    {
        ArgumentNullException.ThrowIfNull(editDraft);

        InitializeComponent();

        DataContext = editDraft;
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
