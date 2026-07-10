using System;
using Shouldly;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation.ViewModels.Tests;

public class RelayCommandTests
{
    [Fact]
    public void Executing_runs_the_supplied_action()
    {
        var executed = false;
        var command = new RelayCommand(() => executed = true);

        command.Execute(null);

        executed.ShouldBeTrue();
    }

    [Fact]
    public void A_command_without_a_predicate_can_always_execute()
    {
        var command = new RelayCommand(() => { });

        command.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public void A_command_can_execute_when_its_predicate_is_satisfied()
    {
        var command = new RelayCommand(() => { }, () => true);

        command.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public void A_command_cannot_execute_when_its_predicate_is_not_satisfied()
    {
        var command = new RelayCommand(() => { }, () => false);

        command.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void Constructing_without_an_action_is_rejected()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new RelayCommand(null!));

        exception.ParamName.ShouldBe("execute");
    }

    [Fact]
    public void Raising_the_can_execute_changed_event_notifies_listeners()
    {
        var command = new RelayCommand(() => { });
        object? sender = null;
        var raised = false;
        command.CanExecuteChanged += (s, _) =>
        {
            sender = s;
            raised = true;
        };

        command.RaiseCanExecuteChanged();

        raised.ShouldBeTrue();
        sender.ShouldBeSameAs(command);
    }

    [Fact]
    public void Raising_the_can_execute_changed_event_without_listeners_does_not_throw()
    {
        var command = new RelayCommand(() => { });

        Should.NotThrow(command.RaiseCanExecuteChanged);
    }
}
