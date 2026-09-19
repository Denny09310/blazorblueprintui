namespace BlazorBlueprint.Components;

/// <summary>
/// Defines what commits the text in a <c>BbTagInput</c> as a tag.
/// Multiple triggers can be combined using bitwise OR.
/// </summary>
[Flags]
public enum TagInputTrigger
{
    /// <summary>Enter key adds the current input as a tag.</summary>
    Enter = 1,

    /// <summary>Comma key adds the current input as a tag.</summary>
    Comma = 2,

    /// <summary>Space key adds the current input as a tag.</summary>
    Space = 4,

    /// <summary>Tab key adds the current input as a tag.</summary>
    Tab = 8,

    /// <summary>Semicolon key adds the current input as a tag.</summary>
    Semicolon = 16,

    /// <summary>
    /// Leaving the input adds whatever was typed as a tag, so a half-finished entry is not lost
    /// when the user clicks or tabs away.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only trigger that is not a key. It commits the text the user typed, not a highlighted
    /// suggestion — clicking a suggestion adds that suggestion and cancels the blur, and returning
    /// focus to the input cancels it too, so neither path can add a tag twice.
    /// </para>
    /// <para>
    /// Text that fails <c>Validate</c>, <c>MaxTags</c>, <c>MaxTagLength</c> or the duplicate check
    /// is left in the input and reported through <c>OnTagRejected</c>, exactly as it is when a key
    /// commits it. Nothing is silently dropped.
    /// </para>
    /// </remarks>
    Blur = 32
}
