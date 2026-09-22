namespace BlazorBlueprint.Primitives.Filtering;

/// <summary>
/// Defines the comparison operator for a filter condition.
/// Available operators depend on the <see cref="FilterFieldType"/> of the field.
/// </summary>
public enum FilterOperator
{
    // Universal
    /// <summary>Matches equal values.</summary>
    Equals,
    /// <summary>Matches unequal values.</summary>
    NotEquals,
    /// <summary>Matches missing or empty values.</summary>
    IsEmpty,
    /// <summary>Matches values that are neither missing nor empty.</summary>
    IsNotEmpty,

    // String
    /// <summary>Matches text containing the filter value.</summary>
    Contains,
    /// <summary>Matches text not containing the filter value.</summary>
    NotContains,
    /// <summary>Matches text starting with the filter value.</summary>
    StartsWith,
    /// <summary>Matches text ending with the filter value.</summary>
    EndsWith,

    // Number / Date comparison
    /// <summary>Matches values greater than the filter value.</summary>
    GreaterThan,
    /// <summary>Matches values less than the filter value.</summary>
    LessThan,
    /// <summary>Matches values greater than or equal to the filter value.</summary>
    GreaterOrEqual,
    /// <summary>Matches values less than or equal to the filter value.</summary>
    LessOrEqual,
    /// <summary>Matches values inside the supplied range.</summary>
    Between,

    // Date-specific
    /// <summary>Matches dates in the preceding relative period.</summary>
    InLast,
    /// <summary>Matches dates in the following relative period.</summary>
    InNext,

    // Enum
    /// <summary>Matches any of the supplied values.</summary>
    In,
    /// <summary>Excludes all of the supplied values.</summary>
    NotIn,

    // Boolean
    /// <summary>Matches true.</summary>
    IsTrue,
    /// <summary>Matches false or null.</summary>
    IsFalse,

    // Date preset
    /// <summary>Matches the supplied date preset.</summary>
    DateIs,
    /// <summary>Excludes the supplied date preset.</summary>
    DateIsNot
}
