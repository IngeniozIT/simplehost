namespace SimpleHost.Application.DeepLinks;

/// <summary>
/// The change a <c>simplehost://</c> deep link asks the app to perform. The verb in a link such as
/// <c>simplehost://add?…</c> selects one of these actions; the specific per-action handling (and any
/// confirmation the safety rules require) is layered on top of the parsed link elsewhere.
/// </summary>
public enum DeepLinkAction
{
    /// <summary>Add a new host entry (<c>simplehost://add</c>).</summary>
    Add,

    /// <summary>Update an existing host entry (<c>simplehost://update</c>).</summary>
    Update,

    /// <summary>Delete a host entry (<c>simplehost://delete</c>).</summary>
    Delete,

    /// <summary>Enable a disabled host entry (<c>simplehost://enable</c>).</summary>
    Enable,

    /// <summary>Disable an active host entry (<c>simplehost://disable</c>).</summary>
    Disable,
}
