namespace BlazorBlueprint.Primitives.SignaturePad;

/// <summary>
/// One continuous pen stroke: everything captured between the pointer going down and coming up.
/// </summary>
/// <param name="Points">The samples that make up the stroke, in the order they were captured.</param>
public sealed record SignatureStroke(IReadOnlyList<SignaturePoint> Points);
