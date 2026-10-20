using Salient.Core;

namespace Salient.Features.Sift;

/// <summary>One octave of the scale space.</summary>
/// <param name="Index">Octave number: −1 for the upsampled octave, then 0, 1, …</param>
/// <param name="Gaussians">s + 3 Gaussian layers.</param>
internal sealed record Octave(int Index, ImageF[] Gaussians)
{
    /// <summary>s + 2 DoG layers; filled by <see cref="DogPyramid"/>.</summary>
    public ImageF[] Dogs { get; set; } = [];

    /// <summary>Size of one octave pixel in input-image pixels: 2^Index.</summary>
    public float PixelSize => MathF.Pow(2, Index);

    public int Width => Gaussians[0].Width;

    public int Height => Gaussians[0].Height;
}

/// <summary>The Gaussian and DoG pyramids of one image.</summary>
internal sealed record ScaleSpace(IReadOnlyList<Octave> Octaves, int LayersPerOctave, double Sigma)
{
    /// <summary>σ of layer <paramref name="layer"/> (possibly fractional) of octave <paramref name="octave"/>, in input-image pixels.</summary>
    public float SigmaAt(Octave octave, float layer) =>
        (float)(Sigma * Math.Pow(2, layer / LayersPerOctave)) * octave.PixelSize;
}

/// <summary>An integer location in the scale space.</summary>
/// <param name="Octave">Index into <see cref="ScaleSpace.Octaves"/> (not the octave number).</param>
/// <param name="Layer">DoG layer, 1..s.</param>
/// <param name="X">Column in octave pixels.</param>
/// <param name="Y">Row in octave pixels.</param>
internal readonly record struct ScaleSpacePoint(int Octave, int Layer, int X, int Y);

/// <summary>A refined extremum.</summary>
/// <param name="Location">Integer location the refinement converged to.</param>
/// <param name="OffsetX">Sub-pixel offset in octave pixels, within ±0.5.</param>
/// <param name="OffsetY">Sub-pixel offset in octave pixels, within ±0.5.</param>
/// <param name="OffsetLayer">Sub-layer offset, within ±0.5.</param>
/// <param name="Value">Interpolated DoG value D(x̂), signed.</param>
/// <param name="Contrast">|D(x̂)| · s, in the units of the contrast threshold.</param>
/// <param name="EdgeRatio">tr²/det of the 2×2 Hessian; NaN until the edge stage computes it.</param>
internal readonly record struct RefinedPoint(
    ScaleSpacePoint Location,
    float OffsetX,
    float OffsetY,
    float OffsetLayer,
    float Value,
    float Contrast,
    float EdgeRatio);
