namespace Salient.Features.Sift;

/// <summary>Stage identifiers reported by <see cref="SiftDetector"/> (ADR-0004).</summary>
public static class SiftStages
{
    /// <summary>Building the Gaussian pyramid.</summary>
    public const string GaussianPyramid = "gaussian-pyramid";

    /// <summary>Building the difference-of-Gaussians pyramid.</summary>
    public const string DifferenceOfGaussians = "dog";

    /// <summary>Finding scale-space extrema.</summary>
    public const string Extrema = "extrema";

    /// <summary>Sub-pixel refinement of extrema.</summary>
    public const string Refine = "refine";

    /// <summary>Contrast threshold.</summary>
    public const string Contrast = "contrast";

    /// <summary>Edge-response rejection.</summary>
    public const string Edge = "edge";

    /// <summary>Orientation assignment.</summary>
    public const string Orientation = "orientation";

    /// <summary>Descriptor computation.</summary>
    public const string Descriptor = "descriptor";

    /// <summary>Heatmaps for the Detector view; only with full pipeline detail.</summary>
    public const string Heatmaps = "heatmaps";
}
