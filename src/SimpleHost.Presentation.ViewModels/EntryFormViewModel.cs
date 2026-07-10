using SimpleHost.Domain.Validation;

namespace SimpleHost.Presentation.ViewModels;

/// <summary>
/// The editable draft of a host entry the view binds to while adding or editing: the IP address,
/// hostname, optional comment and optional category the user is typing. As the IP address and
/// hostname change it re-checks them with the domain validation value objects, so the view can show
/// inline well-formed-versus-invalid feedback and gate saving on <see cref="IsValid"/> before the
/// edit is staged.
/// </summary>
public sealed class EntryFormViewModel : ViewModelBase
{
    private string _ipAddress = string.Empty;
    private string _hostname = string.Empty;
    private string? _comment;
    private string? _category;
    private IpAddressValidation _ipAddressValidation = IpAddressValidation.Of(string.Empty);
    private HostnameValidation _hostnameValidation = HostnameValidation.Of(string.Empty);

    /// <summary>
    /// The IP address being entered. Setting it re-validates the address and refreshes
    /// <see cref="IpAddressValidation"/> and <see cref="IsValid"/>.
    /// </summary>
    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            if (SetProperty(ref _ipAddress, value))
            {
                IpAddressValidation = IpAddressValidation.Of(value);
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }

    /// <summary>
    /// The hostname being entered. Setting it re-validates the hostname and refreshes
    /// <see cref="HostnameValidation"/> and <see cref="IsValid"/>.
    /// </summary>
    public string Hostname
    {
        get => _hostname;
        set
        {
            if (SetProperty(ref _hostname, value))
            {
                HostnameValidation = HostnameValidation.Of(value);
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }

    /// <summary>The optional trailing inline comment being entered, or <c>null</c> for none.</summary>
    public string? Comment
    {
        get => _comment;
        set => SetProperty(ref _comment, value);
    }

    /// <summary>The optional category the entry belongs under, or <c>null</c> for "No category".</summary>
    public string? Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    /// <summary>The current validation of <see cref="IpAddress"/>: whether it is well-formed and why not.</summary>
    public IpAddressValidation IpAddressValidation
    {
        get => _ipAddressValidation;
        private set => SetProperty(ref _ipAddressValidation, value);
    }

    /// <summary>The current validation of <see cref="Hostname"/>: whether it is well-formed and why not.</summary>
    public HostnameValidation HostnameValidation
    {
        get => _hostnameValidation;
        private set => SetProperty(ref _hostnameValidation, value);
    }

    /// <summary>
    /// Whether both the IP address and the hostname are well-formed, so the entry may be saved. The
    /// optional comment and category never make a draft invalid.
    /// </summary>
    public bool IsValid => IpAddressValidation.IsWellFormed && HostnameValidation.IsWellFormed;

    /// <summary>
    /// Clears the draft back to empty, restoring the prompts on <see cref="IpAddressValidation"/> and
    /// <see cref="HostnameValidation"/> so the form is ready for the next entry.
    /// </summary>
    public void Reset()
    {
        IpAddress = string.Empty;
        Hostname = string.Empty;
        Comment = null;
        Category = null;
    }
}
