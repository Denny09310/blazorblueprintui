using System.Diagnostics.CodeAnalysis;

namespace BlazorBlueprint.Components;

/// <summary>
/// How many options a <c>BbListBox</c> lets you choose.
/// </summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name",
    Justification = "Single and Multiple are what every list control calls these; renaming to avoid System.Single would cost more than it saves")]
public enum ListBoxSelectionMode
{
    /// <summary>
    /// One option at a time, bound through <c>Value</c>. Moving with the arrow keys selects as it
    /// goes, which is how a native list behaves.
    /// </summary>
    Single,

    /// <summary>
    /// Any number of options, bound through <c>Values</c>. The arrow keys move without selecting;
    /// Space toggles, Shift extends a range, and Ctrl+A takes everything.
    /// </summary>
    Multiple
}
