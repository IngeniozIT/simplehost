using Shouldly;
using SimpleHost.Presentation.ViewModels.DeepLinks;

namespace SimpleHost.Presentation.ViewModels.Tests.DeepLinks;

public class DeepLinkCommandLineTests
{
    [Fact]
    public void Finds_a_deep_link_argument_among_the_command_line()
    {
        var found = DeepLinkCommandLine.TryExtractLink(
            new[] { "--hosts-file", "C:\\hosts", "simplehost://add?ip=127.0.0.1&hostname=web.test" },
            out var link);

        found.ShouldBeTrue();
        link.ShouldBe("simplehost://add?ip=127.0.0.1&hostname=web.test");
    }

    [Fact]
    public void Reports_no_link_when_no_argument_is_a_simplehost_link()
    {
        var found = DeepLinkCommandLine.TryExtractLink(
            new[] { "--hosts-file", "C:\\hosts" }, out var link);

        found.ShouldBeFalse();
        link.ShouldBeNull();
    }

    [Fact]
    public void Reports_no_link_for_an_empty_command_line()
    {
        var found = DeepLinkCommandLine.TryExtractLink(Array.Empty<string>(), out var link);

        found.ShouldBeFalse();
        link.ShouldBeNull();
    }

    [Fact]
    public void Returns_the_first_deep_link_when_several_are_present()
    {
        var found = DeepLinkCommandLine.TryExtractLink(
            new[]
            {
                "simplehost://delete?hostname=first.test",
                "simplehost://delete?hostname=second.test",
            },
            out var link);

        found.ShouldBeTrue();
        link.ShouldBe("simplehost://delete?hostname=first.test");
    }

    [Fact]
    public void Reports_no_link_for_a_null_argument_list()
    {
        var found = DeepLinkCommandLine.TryExtractLink(null, out var link);

        found.ShouldBeFalse();
        link.ShouldBeNull();
    }
}
