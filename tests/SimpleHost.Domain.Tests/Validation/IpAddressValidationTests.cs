using Shouldly;
using SimpleHost.Domain.Validation;

namespace SimpleHost.Domain.Tests.Validation;

public class IpAddressValidationTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("10.0.0.255")]
    public void A_well_formed_ipv4_address_is_reported_well_formed(string candidate)
    {
        IpAddressValidation.Of(candidate).IsWellFormed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("2001:db8::ff00:42:8329")]
    [InlineData("2001:0db8:0000:0000:0000:ff00:0042:8329")]
    public void A_well_formed_ipv6_address_is_reported_well_formed(string candidate)
    {
        IpAddressValidation.Of(candidate).IsWellFormed.ShouldBeTrue();
    }

    [Fact]
    public void A_well_formed_address_carries_no_warning_message()
    {
        IpAddressValidation.Of("127.0.0.1").Message.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("256.0.0.1")]      // octet out of range
    [InlineData("300.300.300.300")]
    [InlineData("1.2.3")]          // too few octets
    [InlineData("1.2.3.4.5")]      // too many octets
    [InlineData("01.02.03.04")]    // leading zeros
    [InlineData("1.2.3.04")]       // leading zero in one octet
    [InlineData("0x7f.0.0.1")]     // hex shorthand IPAddress.TryParse would accept
    [InlineData("1")]              // single-number shorthand
    [InlineData("12.34.56.789")]   // last octet out of range
    [InlineData("1234.5.6.7")]     // octet longer than three digits
    [InlineData("127.0.0.1 ")]     // trailing whitespace
    [InlineData(" 127.0.0.1")]     // leading whitespace
    [InlineData("1..2.3")]         // empty octet
    [InlineData("1.2.3.-1")]       // negative octet
    [InlineData("999999999999")]   // huge number
    [InlineData("abcd")]           // letters
    [InlineData("hello world")]    // free text
    [InlineData("gggg::1")]        // malformed ipv6
    [InlineData("127.0.0.")]       // trailing dot / empty last octet
    public void A_malformed_candidate_is_reported_not_well_formed(string candidate)
    {
        IpAddressValidation.Of(candidate).IsWellFormed.ShouldBeFalse();
    }

    [Fact]
    public void A_malformed_candidate_is_named_in_the_message()
    {
        var validation = IpAddressValidation.Of("256.0.0.1");

        validation.Message.ShouldContain("256.0.0.1");
        validation.Message.ShouldContain("not a valid IP address");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_candidate_is_not_well_formed_and_prompts_for_input(string? candidate)
    {
        var validation = IpAddressValidation.Of(candidate);

        validation.IsWellFormed.ShouldBeFalse();
        validation.Message.ShouldBe("Enter an IP address.");
    }
}
