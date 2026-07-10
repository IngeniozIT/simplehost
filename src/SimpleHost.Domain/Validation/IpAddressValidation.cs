using System.Net;
using System.Net.Sockets;

namespace SimpleHost.Domain.Validation;

/// <summary>
/// The outcome of checking whether a candidate string is a well-formed IP address, produced as
/// the user types so the UI can indicate validity inline and gate saving the entry on it.
/// </summary>
/// <remarks>
/// A candidate is well-formed when it is a strict dotted-decimal IPv4 address (exactly four
/// octets, each <c>0</c>-<c>255</c> with no leading zeros) or a valid IPv6 address. The lenient
/// shorthand and hexadecimal forms that <see cref="IPAddress.TryParse(string?, out IPAddress?)"/>
/// otherwise accepts (for example <c>1</c>, <c>1.2.3</c>, <c>01.02.03.04</c> or <c>0x7f.0.0.1</c>)
/// are rejected, because a <c>hosts</c> file entry needs an unambiguous address.
/// </remarks>
public sealed record IpAddressValidation
{
    private const string EmptyPrompt = "Enter an IP address.";

    private IpAddressValidation(bool isWellFormed, string message)
    {
        IsWellFormed = isWellFormed;
        Message = message;
    }

    /// <summary>
    /// Whether the candidate is a well-formed IPv4 or IPv6 address and the entry may be saved.
    /// </summary>
    public bool IsWellFormed { get; }

    /// <summary>
    /// A human-readable message for inline display: empty when the candidate is well-formed, a
    /// prompt when nothing has been entered yet, and an explanation naming the offending input
    /// when the candidate is not a valid IP address.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Validates <paramref name="candidate"/> as it is typed.
    /// </summary>
    /// <param name="candidate">The text currently entered in the IP address field.</param>
    /// <returns>
    /// A well-formed result with an empty message for a valid IPv4/IPv6 address; a not-well-formed
    /// result prompting for input when <paramref name="candidate"/> is <c>null</c>, empty or only
    /// whitespace; otherwise a not-well-formed result whose message names the invalid candidate.
    /// </returns>
    public static IpAddressValidation Of(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return new IpAddressValidation(false, EmptyPrompt);
        }

        if (IsWellFormedIpv4(candidate) || IsWellFormedIpv6(candidate))
        {
            return new IpAddressValidation(true, string.Empty);
        }

        return new IpAddressValidation(false, $"'{candidate}' is not a valid IP address.");
    }

    private static bool IsWellFormedIpv4(string candidate)
    {
        var octets = candidate.Split('.');
        if (octets.Length != 4)
        {
            return false;
        }

        foreach (var octet in octets)
        {
            if (!IsWellFormedOctet(octet))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWellFormedOctet(string octet)
    {
        if (octet.Length is < 1 or > 3)
        {
            return false;
        }

        foreach (var digit in octet)
        {
            if (digit is < '0' or > '9')
            {
                return false;
            }
        }

        if (octet.Length > 1 && octet[0] == '0')
        {
            return false;
        }

        return int.Parse(octet) <= 255;
    }

    private static bool IsWellFormedIpv6(string candidate) =>
        IPAddress.TryParse(candidate, out var parsed)
        && parsed.AddressFamily == AddressFamily.InterNetworkV6;
}
