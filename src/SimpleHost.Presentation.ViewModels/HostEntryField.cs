namespace SimpleHost.Presentation.ViewModels;

/// <summary>
/// Identifies which single field of a host entry an inline, in-row edit targets. Used by
/// <see cref="MainViewModel.EditEntryField"/> so the view can commit one edited value at a time while
/// leaving the entry's other fields, its category and its active-or-disabled status untouched.
/// </summary>
public enum HostEntryField
{
    /// <summary>The entry's IP address.</summary>
    IpAddress,

    /// <summary>The entry's hostname.</summary>
    Hostname,

    /// <summary>The entry's optional inline comment, shown as its description.</summary>
    Comment,
}
