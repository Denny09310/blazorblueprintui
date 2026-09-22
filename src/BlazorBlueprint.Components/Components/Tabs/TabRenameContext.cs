namespace BlazorBlueprint.Components;

/// <summary>
/// A tab a reader has just renamed.
/// </summary>
/// <remarks>
/// The tab does not rename itself. Store the new label and hand back a changed collection, or set
/// <see cref="Cancel"/> to refuse it — which is where a list puts its "that name is already taken"
/// rule. A refused rename reopens the editor with what was typed still in it, so the name can be
/// fixed rather than retyped from the start.
/// </remarks>
public sealed class TabRenameContext
{
    /// <summary>Gets the value that identifies the tab.</summary>
    public required string Value { get; init; }

    /// <summary>Gets the label the tab carried before the edit.</summary>
    public required string OldLabel { get; init; }

    /// <summary>Gets the label the reader typed, trimmed of surrounding space.</summary>
    /// <remarks>Never empty. An edit committed with nothing in it is treated as a cancel.</remarks>
    public required string NewLabel { get; init; }

    /// <summary>
    /// Gets or sets whether to refuse the new label and reopen the editor.
    /// </summary>
    public bool Cancel { get; set; }
}
