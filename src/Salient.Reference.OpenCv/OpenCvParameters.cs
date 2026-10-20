using System.ComponentModel.DataAnnotations;
using Salient.Features.Parameters;

namespace Salient.Reference.OpenCv;

/// <summary>Parameters of OpenCV's SIFT; defaults are OpenCV's.</summary>
public sealed record OpenCvSiftParameters
{
    /// <summary>Keep only the best N keypoints; 0 keeps all.</summary>
    [Display(Name = "Max features", Description = "Keep only the best N keypoints. 0 keeps all.")]
    [Range(0, 20000)]
    [Step(100)]
    public int MaxFeatures { get; init; }

    /// <summary>Scales s sampled per octave.</summary>
    [Display(Name = "Layers per octave")]
    [Range(1, 6)]
    [Step(1)]
    public int LayersPerOctave { get; init; } = 3;

    /// <summary>OpenCV's contrast threshold (compared with |D| · s).</summary>
    [Display(Name = "Contrast threshold")]
    [Range(0.0, 0.1)]
    [Step(0.0025)]
    public double ContrastThreshold { get; init; } = 0.04;

    /// <summary>Edge threshold r.</summary>
    [Display(Name = "Edge threshold r")]
    [Range(2.0, 30.0)]
    [Step(1)]
    public double EdgeThreshold { get; init; } = 10;

    /// <summary>σ₀ of the first layer of each octave.</summary>
    [Display(Name = "σ₀")]
    [Range(0.8, 3.2)]
    [Step(0.1)]
    public double Sigma { get; init; } = 1.6;
}

/// <summary>Parameters of OpenCV's ORB.</summary>
public sealed record OrbParameters
{
    /// <summary>Maximum number of keypoints.</summary>
    [Display(Name = "Max features")]
    [Range(100, 20000)]
    [Step(100)]
    public int MaxFeatures { get; init; } = 1500;

    /// <summary>Pyramid decimation ratio.</summary>
    [Display(Name = "Scale factor")]
    [Range(1.05, 2.0)]
    [Step(0.05)]
    public double ScaleFactor { get; init; } = 1.2;

    /// <summary>Number of pyramid levels.</summary>
    [Display(Name = "Levels")]
    [Range(1, 16)]
    [Step(1)]
    public int Levels { get; init; } = 8;

    /// <summary>FAST threshold.</summary>
    [Display(Name = "FAST threshold")]
    [Range(1, 100)]
    [Step(1)]
    public int FastThreshold { get; init; } = 20;
}

/// <summary>Parameters of OpenCV's AKAZE.</summary>
public sealed record AkazeParameters
{
    /// <summary>Detector response threshold.</summary>
    [Display(Name = "Threshold")]
    [Range(0.0001, 0.01)]
    [Step(0.0001)]
    public double Threshold { get; init; } = 0.001;

    /// <summary>Number of octaves.</summary>
    [Display(Name = "Octaves")]
    [Range(1, 8)]
    [Step(1)]
    public int Octaves { get; init; } = 4;

    /// <summary>Sublevels per octave.</summary>
    [Display(Name = "Layers per octave")]
    [Range(1, 8)]
    [Step(1)]
    public int LayersPerOctave { get; init; } = 4;
}
