namespace BlazorBlueprint.Components;

/// <summary>
/// A tab a reader has just dragged, or moved with Ctrl and an arrow key, to a new position.
/// </summary>
/// <remarks>
/// The list does not reorder itself, because only the caller knows whether the order it keeps is a
/// list, a sort field or a column in a database. Apply the move to the collection the tabs are
/// written from, or do nothing — the tabs are drawn from that collection either way, so declining
/// to apply it <em>is</em> the refusal. There is deliberately no Cancel flag, because there is no
/// optimistic state for one to undo.
/// </remarks>
public sealed class TabMoveContext
{
    /// <summary>Gets the value that identifies the tab that moved.</summary>
    public required string Value { get; init; }

    /// <summary>Gets the position the tab held before the move, counting from zero.</summary>
    public required int OldIndex { get; init; }

    /// <summary>Gets the position the tab is being asked to take, counting from zero.</summary>
    /// <remarks>
    /// Already resolved as a final index rather than as an insertion point, so applying the move is
    /// a remove followed by an insert at this number and never an off-by-one.
    /// </remarks>
    public required int NewIndex { get; init; }
}
