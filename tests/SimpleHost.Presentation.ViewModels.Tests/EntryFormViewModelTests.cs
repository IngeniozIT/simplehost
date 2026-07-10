using System.Collections.Generic;
using System.ComponentModel;
using Shouldly;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation.ViewModels.Tests;

public class EntryFormViewModelTests
{
    [Fact]
    public void A_fresh_form_is_empty_and_prompts_for_both_fields()
    {
        var form = new EntryFormViewModel();

        form.IpAddress.ShouldBe(string.Empty);
        form.Hostname.ShouldBe(string.Empty);
        form.Comment.ShouldBeNull();
        form.Category.ShouldBeNull();
        form.IpAddressValidation.IsWellFormed.ShouldBeFalse();
        form.IpAddressValidation.Message.ShouldBe("Enter an IP address.");
        form.HostnameValidation.IsWellFormed.ShouldBeFalse();
        form.HostnameValidation.Message.ShouldBe("Enter a hostname.");
        form.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Setting_a_well_formed_ip_address_marks_it_valid_with_no_message()
    {
        var form = new EntryFormViewModel();

        form.IpAddress = "127.0.0.1";

        form.IpAddressValidation.IsWellFormed.ShouldBeTrue();
        form.IpAddressValidation.Message.ShouldBeEmpty();
    }

    [Fact]
    public void Setting_a_malformed_ip_address_marks_it_invalid_and_names_the_candidate()
    {
        var form = new EntryFormViewModel();

        form.IpAddress = "not-an-ip";

        form.IpAddressValidation.IsWellFormed.ShouldBeFalse();
        form.IpAddressValidation.Message.ShouldBe("'not-an-ip' is not a valid IP address.");
    }

    [Fact]
    public void Setting_a_well_formed_hostname_marks_it_valid_with_no_message()
    {
        var form = new EntryFormViewModel();

        form.Hostname = "api.test";

        form.HostnameValidation.IsWellFormed.ShouldBeTrue();
        form.HostnameValidation.Message.ShouldBeEmpty();
    }

    [Fact]
    public void Setting_a_malformed_hostname_marks_it_invalid_and_names_the_candidate()
    {
        var form = new EntryFormViewModel();

        form.Hostname = "bad_host";

        form.HostnameValidation.IsWellFormed.ShouldBeFalse();
        form.HostnameValidation.Message.ShouldBe("'bad_host' is not a valid hostname.");
    }

    [Fact]
    public void The_draft_is_valid_only_when_both_the_ip_address_and_hostname_are_well_formed()
    {
        var form = new EntryFormViewModel();

        form.IpAddress = "127.0.0.1";
        form.IsValid.ShouldBeFalse();

        form.Hostname = "api.test";
        form.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void A_well_formed_ip_with_a_malformed_hostname_is_not_valid()
    {
        var form = new EntryFormViewModel { IpAddress = "127.0.0.1", Hostname = "bad_host" };

        form.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_malformed_ip_with_a_well_formed_hostname_is_not_valid()
    {
        var form = new EntryFormViewModel { IpAddress = "999.0.0.1", Hostname = "api.test" };

        form.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Editing_the_ip_address_notifies_its_validation_and_the_overall_validity()
    {
        var form = new EntryFormViewModel();
        var changed = Record(form);

        form.IpAddress = "127.0.0.1";

        changed.ShouldContain(nameof(EntryFormViewModel.IpAddress));
        changed.ShouldContain(nameof(EntryFormViewModel.IpAddressValidation));
        changed.ShouldContain(nameof(EntryFormViewModel.IsValid));
    }

    [Fact]
    public void Editing_the_hostname_notifies_its_validation_and_the_overall_validity()
    {
        var form = new EntryFormViewModel();
        var changed = Record(form);

        form.Hostname = "api.test";

        changed.ShouldContain(nameof(EntryFormViewModel.Hostname));
        changed.ShouldContain(nameof(EntryFormViewModel.HostnameValidation));
        changed.ShouldContain(nameof(EntryFormViewModel.IsValid));
    }

    [Fact]
    public void Re_assigning_the_same_ip_address_raises_no_further_notifications()
    {
        var form = new EntryFormViewModel { IpAddress = "127.0.0.1" };
        var changed = Record(form);

        form.IpAddress = "127.0.0.1";

        changed.ShouldBeEmpty();
    }

    [Fact]
    public void Re_assigning_the_same_hostname_raises_no_further_notifications()
    {
        var form = new EntryFormViewModel { Hostname = "api.test" };
        var changed = Record(form);

        form.Hostname = "api.test";

        changed.ShouldBeEmpty();
    }

    [Fact]
    public void Setting_the_comment_and_category_notifies_and_holds_them()
    {
        var form = new EntryFormViewModel();
        var changed = Record(form);

        form.Comment = "local api";
        form.Category = "Development";

        form.Comment.ShouldBe("local api");
        form.Category.ShouldBe("Development");
        changed.ShouldContain(nameof(EntryFormViewModel.Comment));
        changed.ShouldContain(nameof(EntryFormViewModel.Category));
    }

    [Fact]
    public void Reset_clears_every_field_and_restores_the_prompts()
    {
        var form = new EntryFormViewModel
        {
            IpAddress = "127.0.0.1",
            Hostname = "api.test",
            Comment = "local api",
            Category = "Development",
        };

        form.Reset();

        form.IpAddress.ShouldBe(string.Empty);
        form.Hostname.ShouldBe(string.Empty);
        form.Comment.ShouldBeNull();
        form.Category.ShouldBeNull();
        form.IpAddressValidation.IsWellFormed.ShouldBeFalse();
        form.IpAddressValidation.Message.ShouldBe("Enter an IP address.");
        form.HostnameValidation.IsWellFormed.ShouldBeFalse();
        form.HostnameValidation.Message.ShouldBe("Enter a hostname.");
        form.IsValid.ShouldBeFalse();
    }

    private static List<string?> Record(EntryFormViewModel form)
    {
        var changed = new List<string?>();
        form.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        return changed;
    }
}
