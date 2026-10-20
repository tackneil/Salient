namespace Salient.Features;

/// <summary>What a detector returns besides keypoints.</summary>
/// <param name="IncludeDiagnostics">Return <see cref="KeypointDiagnostics"/> aligned with the keypoints.</param>
public sealed record DetectionOptions(bool IncludeDiagnostics)
{
    /// <summary>Keypoints only.</summary>
    public static DetectionOptions Default { get; } = new(IncludeDiagnostics: false);

    /// <summary>Keypoints with diagnostics, as used by interactive runs (ADR-0003).</summary>
    public static DetectionOptions WithDiagnostics { get; } = new(IncludeDiagnostics: true);
}

/// <summary>Result of a detection.</summary>
/// <param name="Keypoints">Detected keypoints.</param>
/// <param name="Diagnostics">Per-keypoint diagnostics aligned by index with <paramref name="Keypoints"/>, or <see langword="null"/> when not requested.</param>
public sealed record DetectionResult(IReadOnlyList<Keypoint> Keypoints, IReadOnlyList<KeypointDiagnostics>? Diagnostics);

/// <summary>
/// Measurements behind one keypoint, used by the keypoint inspector and for client-side threshold filtering (ADR-0003).
/// Each algorithm fills only the fields it has.
/// </summary>
/// <param name="Contrast">
/// Contrast in the units of the algorithm's contrast-threshold parameter: a keypoint survives threshold t exactly when Contrast ≥ t.
/// For SIFT this is |D(x̂)| · s.
/// </param>
/// <param name="EdgeRatio">
/// tr²(H) / det(H) of the 2×2 Hessian, or positive infinity when det(H) ≤ 0.
/// A keypoint survives edge threshold r exactly when EdgeRatio &lt; (r + 1)² / r.
/// </param>
/// <param name="OrientationHistogram">
/// The smoothed orientation histogram the keypoint's angle was taken from, scaled so its highest bin is 1.
/// Bin i is centered at i × 360° / length, counterclockwise.
/// </param>
public sealed record KeypointDiagnostics(float? Contrast, float? EdgeRatio, IReadOnlyList<float>? OrientationHistogram);
