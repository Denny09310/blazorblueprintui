namespace BlazorBlueprint.Components;

/// <summary>
/// The one definition of what a list search matches.
/// </summary>
/// <remarks>
/// <c>BbPickList</c> has to know which options a pane's search has left visible, so that its
/// move-everything buttons act on those and not on the hidden rest. Two copies of the rule would
/// drift, and the symptom would be a button quietly moving rows the person could not see.
/// </remarks>
internal static class ListBoxSearch
{
    public static bool Matches(string text, string? query) =>
        string.IsNullOrEmpty(query)
        || text.Contains(query, StringComparison.CurrentCultureIgnoreCase);
}
