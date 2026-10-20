using System.ComponentModel.DataAnnotations;
using Salient.Features.Parameters;

namespace Salient.Features.Sift;

/// <summary>Parameters of the SIFT detector. Defaults follow Lowe (2004) and OpenCV.</summary>
public sealed record SiftParameters
{
    /// <summary>Lowest contrast threshold the UI offers; used by <see cref="Permissive"/>.</summary>
    public const double MinContrastThreshold = 0.005;

    /// <summary>Highest edge threshold the UI offers; used by <see cref="Permissive"/>.</summary>
    public const double MaxEdgeThreshold = 30;

    /// <summary>Number of octaves; 0 uses as many as the image size allows.</summary>
    [Display(Name = "Octaves", Description = "Number of octaves. 0 uses as many as the image size allows.", GroupName = "Scale space")]
    [Range(0, 10)]
    [Step(1)]
    public int Octaves { get; init; }

    /// <summary>Number of scales s sampled per octave; each octave holds s + 3 Gaussian and s + 2 DoG layers.</summary>
    [Display(Name = "Layers per octave", Description = "Scales s sampled per octave.", GroupName = "Scale space")]
    [Range(1, 6)]
    [Step(1)]
    public int LayersPerOctave { get; init; } = 3;

    /// <summary>σ₀ of the first layer of every octave, relative to that octave.</summary>
    [Display(Name = "σ₀", Description = "Blur of the first layer of each octave.", GroupName = "Scale space")]
    [Range(0.8, 3.2)]
    [Step(0.1)]
    public double Sigma { get; init; } = 1.6;

    /// <summary>Rejects keypoints with |D(x̂)| · s below this value (intensities in [0, 1]).</summary>
    [Display(Name = "Contrast threshold", Description = "Rejects weak extrema: |D(x̂)| below the threshold.", GroupName = "Filters")]
    [Range(MinContrastThreshold, 0.1)]
    [Step(0.0025)]
    public double ContrastThreshold { get; init; } = 0.04;

    /// <summary>Rejects edge responses: tr²(H) / det(H) ≥ (r + 1)² / r.</summary>
    [Display(Name = "Edge threshold r", Description = "Rejects edge responses: tr²(H) / det(H) ≥ (r+1)² / r.", GroupName = "Filters")]
    [Range(2.0, MaxEdgeThreshold)]
    [Step(1)]
    public double EdgeThreshold { get; init; } = 10;

    /// <summary>Doubles the image before the first octave (Lowe's octave −1). Finds more small keypoints at about four times the cost.</summary>
    [Display(Name = "Upsample input", Description = "Double the image before the first octave. More small keypoints, about 4× slower.", GroupName = "Scale space")]
    [Advanced]
    public bool UpsampleInput { get; init; }

    /// <summary>
    /// The same parameters with the most permissive thresholds the UI allows. Interactive runs use it so the client can filter
    /// by contrast and edge ratio without a new run (ADR-0003).
    /// </summary>
    public SiftParameters Permissive() =>
        this with { ContrastThreshold = MinContrastThreshold, EdgeThreshold = MaxEdgeThreshold };

    /// <summary>Throws <see cref="ValidationException"/> when a value is outside its declared range.</summary>
    public void Validate() => Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);
}
