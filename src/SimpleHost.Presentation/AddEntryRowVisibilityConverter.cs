using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SimpleHost.Presentation;

/// <summary>
/// Decides whether the inline "new entry" draft row is shown under a given category group. It takes
/// three bound values — whether a draft is currently open
/// (<see cref="ViewModels.MainViewModel.IsAddingEntry"/>), the category the draft targets
/// (<see cref="ViewModels.MainViewModel.AddingEntryCategory"/>, <c>null</c> for the "No category"
/// group), and this group's own name — and yields <see cref="Visibility.Visible"/> only for the one
/// group the draft belongs to while a draft is open, and <see cref="Visibility.Collapsed"/> for every
/// other group and whenever no draft is open. The open flag is checked explicitly so a draft targeting
/// the "No category" group (whose name is <c>null</c>) is distinguished from no draft being open.
/// </summary>
public sealed class AddEntryRowVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [bool isAddingEntry, var addingCategory, var groupName])
        {
            return Visibility.Collapsed;
        }

        return isAddingEntry
            && string.Equals(addingCategory as string, groupName as string, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
