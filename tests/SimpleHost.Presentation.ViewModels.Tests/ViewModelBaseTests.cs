using System.Collections.Generic;
using System.ComponentModel;
using Shouldly;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation.ViewModels.Tests;

public class ViewModelBaseTests
{
    [Fact]
    public void Setting_a_property_to_a_new_value_stores_it()
    {
        var viewModel = new TestViewModel();

        viewModel.Name = "changed";

        viewModel.Name.ShouldBe("changed");
    }

    [Fact]
    public void Setting_a_property_to_a_new_value_reports_that_it_changed()
    {
        var viewModel = new TestViewModel();

        viewModel.SetName("changed").ShouldBeTrue();
    }

    [Fact]
    public void Setting_a_property_to_a_new_value_raises_change_notification_for_that_property()
    {
        var viewModel = new TestViewModel();
        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        viewModel.Name = "changed";

        raised.ShouldBe(new[] { nameof(TestViewModel.Name) });
    }

    [Fact]
    public void Setting_a_property_to_the_same_value_reports_no_change()
    {
        var viewModel = new TestViewModel();
        viewModel.Name = "same";

        viewModel.SetName("same").ShouldBeFalse();
    }

    [Fact]
    public void Setting_a_property_to_the_same_value_raises_no_change_notification()
    {
        var viewModel = new TestViewModel();
        viewModel.Name = "same";
        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, args) => raised.Add(args.PropertyName);

        viewModel.Name = "same";

        raised.ShouldBeEmpty();
    }

    [Fact]
    public void Setting_a_property_to_the_same_value_leaves_the_stored_value_untouched()
    {
        var viewModel = new TestViewModel();
        viewModel.Name = "same";

        viewModel.SetName("same");

        viewModel.Name.ShouldBe("same");
    }

    [Fact]
    public void Raising_a_notification_carries_the_supplied_property_name()
    {
        var viewModel = new TestViewModel();
        PropertyChangedEventArgs? captured = null;
        viewModel.PropertyChanged += (_, args) => captured = args;

        viewModel.RaiseChangedFor("Anything");

        captured.ShouldNotBeNull();
        captured!.PropertyName.ShouldBe("Anything");
    }

    [Fact]
    public void Raising_a_notification_without_listeners_does_not_throw()
    {
        var viewModel = new TestViewModel();

        Should.NotThrow(() => viewModel.RaiseChangedFor("Anything"));
    }

    private sealed class TestViewModel : ViewModelBase
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public bool SetName(string value) => SetProperty(ref _name, value, nameof(Name));

        public void RaiseChangedFor(string propertyName) => OnPropertyChanged(propertyName);
    }
}
