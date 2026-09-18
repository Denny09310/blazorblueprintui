namespace BlazorBlueprint.Components;

/// <summary>
/// Exposes a hierarchy column's indent to the grid that hosts it.
/// <para>
/// The grid holds its hierarchy column as <c>IDataGridColumn&lt;TData&gt;</c>, and the column's own
/// type carries a second type parameter for the bound property, so the grid cannot name it. This
/// narrow interface is how the grid reads the one value it needs — which is why
/// <c>IndentSize</c> went unread, and every level indented by a hard-coded 24 pixels.
/// </para>
/// </summary>
internal interface IHierarchyIndentColumn
{
    /// <summary>Indentation per depth level, in pixels.</summary>
    public int IndentSize { get; }
}
