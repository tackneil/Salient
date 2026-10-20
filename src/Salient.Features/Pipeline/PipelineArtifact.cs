using Salient.Core;

namespace Salient.Features.Pipeline;

/// <summary>An intermediate result reported with <see cref="PipelineDetail.Full"/>.</summary>
public abstract record PipelineArtifact;

/// <summary>Kind of an image layer artifact.</summary>
public enum ImageLayerKind
{
    /// <summary>A Gaussian-blurred layer L(x, y, σ).</summary>
    Gaussian,

    /// <summary>A signed difference-of-Gaussians layer D(x, y, σ).</summary>
    DifferenceOfGaussians,
}

/// <summary>One image layer of a scale-space pyramid.</summary>
/// <param name="Octave">Octave index; −1 for an upsampled first octave.</param>
/// <param name="Layer">Layer index within the octave.</param>
/// <param name="Kind">Gaussian or DoG layer.</param>
/// <param name="Sigma">σ of the layer in input-image pixels; for DoG, the σ of the lower Gaussian layer.</param>
/// <param name="Image">The layer in octave resolution. Shared with the algorithm: do not modify it.</param>
public sealed record ImageLayerArtifact(int Octave, int Layer, ImageLayerKind Kind, double Sigma, ImageF Image) : PipelineArtifact;

/// <summary>Why a candidate was discarded.</summary>
public enum RejectionReason
{
    /// <summary>Not rejected.</summary>
    None,

    /// <summary>Sub-pixel refinement did not converge, left the valid range, or met a singular Hessian.</summary>
    Unstable,

    /// <summary>Contrast below the threshold.</summary>
    LowContrast,

    /// <summary>Response lies on an edge (ratio of principal curvatures too large, or curvatures of opposite sign).</summary>
    EdgeResponse,
}

/// <summary>A candidate point as seen by one stage.</summary>
/// <param name="X">Horizontal position in input-image pixels.</param>
/// <param name="Y">Vertical position in input-image pixels.</param>
/// <param name="Scale">σ in input-image pixels.</param>
/// <param name="Octave">Octave index.</param>
/// <param name="Contrast">Contrast measure used by the contrast threshold, or <see cref="float.NaN"/> before it is known.</param>
/// <param name="EdgeRatio">tr²(H) / det(H) of the 2×2 Hessian, positive infinity when det(H) ≤ 0, or <see cref="float.NaN"/> before it is known.</param>
/// <param name="Rejection">Why the stage rejected the candidate, or <see cref="RejectionReason.None"/>.</param>
public readonly record struct PipelineCandidate(float X, float Y, float Scale, int Octave, float Contrast, float EdgeRatio, RejectionReason Rejection);

/// <summary>Kind of a heatmap artifact.</summary>
public enum HeatmapKind
{
    /// <summary>Signed DoG response D(x, y, σ).</summary>
    DogResponse,

    /// <summary>Gradient magnitude |∇L| of the Gaussian layer.</summary>
    GradientMagnitude,

    /// <summary>Contrast |D| in the units of the contrast threshold.</summary>
    Contrast,

    /// <summary>Edge ratio tr²(H) / det(H) of the DoG Hessian; positive infinity where det(H) ≤ 0.</summary>
    EdgeRatio,
}

/// <summary>A per-pixel map at input resolution, shown as a heatmap overlay in the Detector view.</summary>
/// <param name="Kind">What the map measures.</param>
/// <param name="Sigma">σ, in input-image pixels, of the scale the map was computed at.</param>
/// <param name="Map">The map. Owned by the receiver.</param>
public sealed record HeatmapArtifact(HeatmapKind Kind, double Sigma, ImageF Map) : PipelineArtifact;

/// <summary>All candidates a stage looked at, kept and rejected.</summary>
/// <param name="Candidates">Candidates; rejected ones carry a <see cref="RejectionReason"/>.</param>
public sealed record CandidateSetArtifact(IReadOnlyList<PipelineCandidate> Candidates) : PipelineArtifact;
