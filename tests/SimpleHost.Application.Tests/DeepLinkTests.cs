using Shouldly;
using SimpleHost.Application.DeepLinks;

namespace SimpleHost.Application.Tests;

public class DeepLinkTests
{
    [Fact]
    public void Parses_an_add_link_into_the_add_action()
    {
        DeepLink.TryParse("simplehost://add?ip=127.0.0.1&hostname=web.test", out var link).ShouldBeTrue();

        link!.Action.ShouldBe(DeepLinkAction.Add);
    }

    [Fact]
    public void Parses_an_update_link_into_the_update_action()
    {
        DeepLink.TryParse("simplehost://update?hostname=web.test&ip=10.0.0.1", out var link).ShouldBeTrue();

        link!.Action.ShouldBe(DeepLinkAction.Update);
    }

    [Fact]
    public void Parses_a_delete_link_into_the_delete_action()
    {
        DeepLink.TryParse("simplehost://delete?hostname=web.test", out var link).ShouldBeTrue();

        link!.Action.ShouldBe(DeepLinkAction.Delete);
    }

    [Fact]
    public void Parses_an_enable_link_into_the_enable_action()
    {
        DeepLink.TryParse("simplehost://enable?hostname=web.test", out var link).ShouldBeTrue();

        link!.Action.ShouldBe(DeepLinkAction.Enable);
    }

    [Fact]
    public void Parses_a_disable_link_into_the_disable_action()
    {
        DeepLink.TryParse("simplehost://disable?hostname=web.test", out var link).ShouldBeTrue();

        link!.Action.ShouldBe(DeepLinkAction.Disable);
    }

    [Fact]
    public void Recognises_the_action_verb_regardless_of_its_letter_case()
    {
        DeepLink.TryParse("simplehost://ADD?ip=127.0.0.1&hostname=web.test", out var link).ShouldBeTrue();

        link!.Action.ShouldBe(DeepLinkAction.Add);
    }

    [Fact]
    public void Reads_the_query_parameters_as_named_values()
    {
        DeepLink.TryParse(
            "simplehost://add?ip=127.0.0.1&hostname=web.test&category=Work&comment=frontend",
            out var link).ShouldBeTrue();

        link!.Parameters["ip"].ShouldBe("127.0.0.1");
        link.Parameters["hostname"].ShouldBe("web.test");
        link.Parameters["category"].ShouldBe("Work");
        link.Parameters["comment"].ShouldBe("frontend");
    }

    [Fact]
    public void Matches_parameter_names_regardless_of_their_letter_case()
    {
        DeepLink.TryParse("simplehost://add?IP=127.0.0.1&HostName=web.test", out var link).ShouldBeTrue();

        link!.Parameters["ip"].ShouldBe("127.0.0.1");
        link.Parameters["hostname"].ShouldBe("web.test");
    }

    [Fact]
    public void Percent_decodes_parameter_values()
    {
        DeepLink.TryParse("simplehost://add?comment=local%20frontend%20server", out var link).ShouldBeTrue();

        link!.Parameters["comment"].ShouldBe("local frontend server");
    }

    [Fact]
    public void Percent_decodes_parameter_names()
    {
        DeepLink.TryParse("simplehost://add?host%6Eame=web.test", out var link).ShouldBeTrue();

        link!.Parameters["hostname"].ShouldBe("web.test");
    }

    [Fact]
    public void A_link_without_a_query_has_no_parameters()
    {
        DeepLink.TryParse("simplehost://delete", out var link).ShouldBeTrue();

        link!.Parameters.ShouldBeEmpty();
    }

    [Fact]
    public void A_parameter_written_without_a_value_reads_as_an_empty_string()
    {
        DeepLink.TryParse("simplehost://add?ip=127.0.0.1&comment", out var link).ShouldBeTrue();

        link!.Parameters["comment"].ShouldBe(string.Empty);
    }

    [Fact]
    public void Ignores_empty_segments_in_the_query()
    {
        DeepLink.TryParse("simplehost://add?&ip=127.0.0.1&&hostname=web.test&", out var link).ShouldBeTrue();

        link!.Parameters.Count.ShouldBe(2);
        link.Parameters["ip"].ShouldBe("127.0.0.1");
        link.Parameters["hostname"].ShouldBe("web.test");
    }

    [Fact]
    public void Ignores_a_segment_that_has_no_name()
    {
        DeepLink.TryParse("simplehost://add?=orphan&ip=127.0.0.1", out var link).ShouldBeTrue();

        link!.Parameters.Count.ShouldBe(1);
        link.Parameters["ip"].ShouldBe("127.0.0.1");
    }

    [Fact]
    public void When_a_parameter_name_repeats_the_last_value_wins()
    {
        DeepLink.TryParse("simplehost://add?ip=127.0.0.1&ip=10.0.0.1", out var link).ShouldBeTrue();

        link!.Parameters["ip"].ShouldBe("10.0.0.1");
    }

    [Fact]
    public void Rejects_a_link_whose_scheme_is_not_simplehost()
    {
        DeepLink.TryParse("https://add?hostname=web.test", out var link).ShouldBeFalse();

        link.ShouldBeNull();
    }

    [Fact]
    public void Rejects_a_link_whose_action_is_not_recognised()
    {
        DeepLink.TryParse("simplehost://frobnicate?hostname=web.test", out var link).ShouldBeFalse();

        link.ShouldBeNull();
    }

    [Fact]
    public void Rejects_a_link_that_names_no_action()
    {
        DeepLink.TryParse("simplehost://?hostname=web.test", out var link).ShouldBeFalse();

        link.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_or_blank_link(string? candidate)
    {
        DeepLink.TryParse(candidate, out var link).ShouldBeFalse();

        link.ShouldBeNull();
    }

    [Theory]
    [InlineData("not a link at all")]
    [InlineData("add?hostname=web.test")]
    public void Rejects_text_that_is_not_an_absolute_link(string candidate)
    {
        DeepLink.TryParse(candidate, out var link).ShouldBeFalse();

        link.ShouldBeNull();
    }

    [Fact]
    public void Exposes_the_shared_scheme_name()
    {
        DeepLink.Scheme.ShouldBe("simplehost");
    }
}
