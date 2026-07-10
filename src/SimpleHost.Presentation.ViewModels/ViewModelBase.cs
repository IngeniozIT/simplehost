using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleHost.Presentation.ViewModels;

/// <summary>
/// Cross-platform base class for view-models. Implements
/// <see cref="INotifyPropertyChanged"/> using only base-class-library types, so it
/// builds and is tested on Debian/WSL without any dependency on WPF.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises <see cref="PropertyChanged"/> for the given property. The caller's member
    /// name is supplied automatically when the argument is omitted.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Assigns <paramref name="value"/> to <paramref name="field"/> and raises a change
    /// notification, but only when the value actually changed. Returns <c>true</c> when a
    /// change was made, <c>false</c> when the value was already equal.
    /// </summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
