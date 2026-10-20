using Salient.Core;
using Salient.Matching;

namespace Salient.Evaluation;

/// <summary>A family of image transformations swept by the robustness bench.</summary>
public enum TransformationFamily
{
    /// <summary>Rotation around the image center, in degrees counterclockwise on screen.</summary>
    Rotation,

    /// <summary>Uniform scaling around the image center.</summary>
    Scale,

    /// <summary>Gaussian blur with σ in pixels.</summary>
    Blur,

    /// <summary>Additive Gaussian noise with σ in gray levels (0–255).</summary>
    Noise,

    /// <summary>Brightness shift in gray levels (−255–255).</summary>
    Brightness,
}

/// <summary>A transformed image and the ground-truth mapping from reference to transformed coordinates.</summary>
/// <param name="Image">The transformed image, same size as the reference.</param>
/// <param name="GroundTruth">Maps reference coordinates to transformed coordinates.</param>
public sealed record TransformedImage(ImageF Image, Homography GroundTruth);

/// <summary>Synthetic image transformations with known ground truth.</summary>
public static class Transformations
{
    /// <summary>Default sweep values per family, matching the design mockup.</summary>
    public static IReadOnlyDictionary<TransformationFamily, double[]> DefaultValues { get; } = new Dictionary<TransformationFamily, double[]>
    {
        [TransformationFamily.Rotation] = [0, 15, 30, 45, 60, 75, 90, 120, 150, 180],
        [TransformationFamily.Scale] = [0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.25, 1.5, 1.75, 2.0],
        [TransformationFamily.Blur] = [0, 1, 2, 3, 4, 5, 6, 8],
        [TransformationFamily.Noise] = [0, 5, 10, 15, 20, 30, 40],
        [TransformationFamily.Brightness] = [-60, -40, -20, 0, 20, 40, 60],
    };

    /// <summary>The value at which a family leaves the image unchanged.</summary>
    public static double IdentityValue(TransformationFamily family) => family == TransformationFamily.Scale ? 1 : 0;

    /// <summary>
    /// Applies one transformation. Geometric families keep the canvas size; uncovered pixels are 0.
    /// Noise is seeded, so the same <paramref name="seed"/> gives the same image.
    /// </summary>
    public static TransformedImage Apply(ImageF reference, TransformationFamily family, double value, int seed = 1)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var center = new Point2((reference.Width - 1) / 2.0, (reference.Height - 1) / 2.0);
        switch (family)
        {
            case TransformationFamily.Rotation:
            case TransformationFamily.Scale:
                var map = family == TransformationFamily.Rotation
                    ? Homography.Similarity(value, 1, center)
                    : Homography.Similarity(0, value, center);
                return new TransformedImage(Quantize(ImageWarp.Warp(reference, map, reference.Width, reference.Height)), map);

            case TransformationFamily.Blur:
                var blurred = value > 0 ? GaussianBlur.Apply(reference, value) : reference.Clone();
                return new TransformedImage(Quantize(blurred), Homography.Identity);

            case TransformationFamily.Noise:
                return new TransformedImage(Quantize(AddNoise(reference, value / 255, seed)), Homography.Identity);

            case TransformationFamily.Brightness:
                var shifted = reference.Clone();
                var shift = (float)(value / 255);
                var pixels = shifted.Pixels;
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] += shift;
                }

                return new TransformedImage(Quantize(shifted), Homography.Identity);

            default:
                throw new ArgumentOutOfRangeException(nameof(family), family, null);
        }
    }

    private static ImageF AddNoise(ImageF image, double sigma, int seed)
    {
        var random = new Random(seed);
        var result = image.Clone();
        var pixels = result.Pixels;
        for (var i = 0; i < pixels.Length; i++)
        {
            // Box–Muller transform.
            var u = 1 - random.NextDouble();
            var v = random.NextDouble();
            pixels[i] += (float)(sigma * Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v));
        }

        return result;
    }

    /// <summary>Rounds to 8 bits like a saved photo, so clipping and quantization effects are part of the test.</summary>
    private static ImageF Quantize(ImageF image) => ImageF.FromGray8(image.ToGray8(), image.Width, image.Height);
}
