using System;
using System.Windows.Input;

namespace SimpleHost.Presentation.ViewModels;

/// <summary>
/// Cross-platform <see cref="ICommand"/> implementation that delegates execution to a
/// supplied action and, optionally, gates it with a predicate. <see cref="ICommand"/>
/// lives in the base class library, so this primitive has no dependency on WPF and is
/// exercised on Debian/WSL.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    /// <summary>Returns whether the command may run; always true when no predicate was given.</summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <summary>Runs the supplied action.</summary>
    public void Execute(object? parameter) => _execute();

    /// <summary>Notifies listeners that <see cref="CanExecute"/> may have changed.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
