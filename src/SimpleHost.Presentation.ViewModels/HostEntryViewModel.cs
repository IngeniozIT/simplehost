using SimpleHost.Domain.Entries;

namespace SimpleHost.Presentation.ViewModels;

/// <summary>
/// A single host entry as presented to the window: its IP address, hostname, optional inline
/// comment, and whether it is currently active (resolving) or disabled (commented out). A read-only
/// projection of a <see cref="HostEntry"/> that the view binds to.
/// </summary>
public sealed class HostEntryViewModel(HostEntry entry)
{
    /// <summary>The IP address the hostname resolves to.</summary>
    public string IpAddress { get; } = entry.IpAddress;

    /// <summary>The hostname being mapped.</summary>
    public string Hostname { get; } = entry.Hostname;

    /// <summary>The trailing inline comment, or <c>null</c> when the entry carries none.</summary>
    public string? Comment { get; } = entry.Comment;

    /// <summary>Whether the entry is active or disabled.</summary>
    public HostEntryStatus Status { get; } = entry.Status;

    /// <summary>
    /// Whether the entry is active (resolving). <c>false</c> when it is disabled (commented out).
    /// Exposed as a bool so the view can bind an active-versus-disabled visual distinction directly.
    /// </summary>
    public bool IsActive => Status == HostEntryStatus.Active;
}
