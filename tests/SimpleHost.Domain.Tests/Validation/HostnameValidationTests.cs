using Shouldly;
using SimpleHost.Domain.Validation;

namespace SimpleHost.Domain.Tests.Validation;

public class HostnameValidationTests
{
    [Theory]
    [InlineData("localhost")]
    [InlineData("a")]
    [InlineData("z")]
    [InlineData("A")]
    [InlineData("Z")]
    [InlineData("0")]
    [InlineData("9")]
    [InlineData("host0")]
    [InlineData("example.com")]
    [InlineData("www.example.com")]
    [InlineData("my-host")]
    [InlineData("a-b-c.d-e-f")]
    [InlineData("host1")]
    [InlineData("1host")]
    [InlineData("EXAMPLE.COM")]
    [InlineData("MixedCase.Example")]
    [InlineData("xn--nxasmq6b.example")]
    [InlineData("a.b.c.d.e.f.g")]
    public void A_well_formed_hostname_is_reported_well_formed(string candidate)
    {
        HostnameValidation.Of(candidate).IsWellFormed.ShouldBeTrue();
    }

    [Fact]
    public void A_label_of_the_maximum_length_is_well_formed()
    {
        var label = new string('a', 63);

        HostnameValidation.Of(label).IsWellFormed.ShouldBeTrue();
    }

    [Fact]
    public void A_label_longer_than_the_maximum_length_is_not_well_formed()
    {
        var label = new string('a', 64);

        HostnameValidation.Of(label).IsWellFormed.ShouldBeFalse();
    }

    [Fact]
    public void A_hostname_of_the_maximum_total_length_is_well_formed()
    {
        // 63 + 1 + 63 + 1 + 63 + 1 + 61 = 253 characters.
        var candidate = string.Join(
            '.',
            new string('a', 63),
            new string('b', 63),
            new string('c', 63),
            new string('d', 61));

        candidate.Length.ShouldBe(253);
        HostnameValidation.Of(candidate).IsWellFormed.ShouldBeTrue();
    }

    [Fact]
    public void A_hostname_longer_than_the_maximum_total_length_is_not_well_formed()
    {
        // 63 + 1 + 63 + 1 + 63 + 1 + 62 = 254 characters, every label individually valid.
        var candidate = string.Join(
            '.',
            new string('a', 63),
            new string('b', 63),
            new string('c', 63),
            new string('d', 62));

        candidate.Length.ShouldBe(254);
        HostnameValidation.Of(candidate).IsWellFormed.ShouldBeFalse();
    }

    [Fact]
    public void A_well_formed_hostname_carries_no_warning_message()
    {
        HostnameValidation.Of("example.com").Message.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("-host")]            // label starts with a hyphen
    [InlineData("host-")]            // label ends with a hyphen
    [InlineData("a.-b")]             // second label starts with a hyphen
    [InlineData("a.b-")]             // second label ends with a hyphen
    [InlineData("host_name")]        // underscore is not allowed
    [InlineData("host name")]        // space is not allowed
    [InlineData("host!")]            // punctuation is not allowed
    [InlineData("ex@mple.com")]      // at-sign is not allowed
    [InlineData("héllo")]            // non-ascii letter is not allowed
    [InlineData(".example.com")]     // leading dot / empty first label
    [InlineData("example.com.")]     // trailing dot / empty last label
    [InlineData("example..com")]     // consecutive dots / empty middle label
    [InlineData(".")]                // lone dot / two empty labels
    [InlineData("example.com ")]     // trailing whitespace
    [InlineData(" example.com")]     // leading whitespace
    [InlineData("under_score.com")]  // underscore in a multi-label name
    [InlineData("*.example.com")]    // wildcard is not allowed
    public void A_malformed_candidate_is_reported_not_well_formed(string candidate)
    {
        HostnameValidation.Of(candidate).IsWellFormed.ShouldBeFalse();
    }

    [Fact]
    public void A_malformed_candidate_is_named_in_the_message()
    {
        var validation = HostnameValidation.Of("host_name");

        validation.Message.ShouldContain("host_name");
        validation.Message.ShouldContain("not a valid hostname");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_candidate_is_not_well_formed_and_prompts_for_input(string? candidate)
    {
        var validation = HostnameValidation.Of(candidate);

        validation.IsWellFormed.ShouldBeFalse();
        validation.Message.ShouldBe("Enter a hostname.");
    }
}
