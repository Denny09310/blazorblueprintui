using System.Diagnostics.CodeAnalysis;

namespace BlazorBlueprint.Components;

/// <summary>
/// A move between the two panes of a <c>BbPickList</c>.
/// </summary>
/// <typeparam name="TValue">The type of the option values.</typeparam>
/// <param name="Values">The values that moved.</param>
/// <param name="ToTarget">
/// <c>true</c> when they moved into the selected pane, <c>false</c> when they moved back out.
/// </param>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Type represents event arguments")]
public readonly record struct PickListMoveEventArgs<TValue>(IReadOnlyList<TValue> Values, bool ToTarget);
