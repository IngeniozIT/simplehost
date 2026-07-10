using System.Diagnostics.CodeAnalysis;
using SimpleHost.Application.DeepLinks;

namespace SimpleHost.Presentation.ViewModels.DeepLinks;

/// <summary>
/// Finds the <c>simplehost://</c> deep link, if any, among the command-line arguments the operating
/// system launches (or re-activates) the app with. When a user opens such a link Windows starts
/// SimpleHost passing the raw link as an argument; this isolates the "which argument is the link"
/// decision from the WPF shell and keeps it testable, and it stays total — arguments that are not a
/// valid link (the hosts-file switches, stray text) are simply skipped rather than throwing.
/// </summary>
public static class DeepLinkCommandLine
{
    /// <summary>
    /// Returns the first argument in <paramref name="args"/> that parses as a valid
    /// <c>simplehost://</c> deep link, or <c>false</c> when none does.
    /// </summary>
    /// <param name="args">The command-line arguments, as received from the operating system.</param>
    /// <param name="link">The matched raw link when one is found; otherwise <c>null</c>.</param>
    public static bool TryExtractLink(
        IEnumerable<string>? args, [NotNullWhen(true)] out string? link)
    {
        link = null;
        if (args is null)
        {
            return false;
        }

        foreach (var arg in args)
        {
            if (DeepLink.TryParse(arg, out _))
            {
                link = arg;
                return true;
            }
        }

        return false;
    }
}
