namespace BlazorBlueprint.Components;

/// <summary>
/// A geographic position expressed in decimal degrees.
/// </summary>
public readonly record struct BbMapCoordinate(double Latitude, double Longitude);