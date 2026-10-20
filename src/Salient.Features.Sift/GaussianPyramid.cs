using Salient.Core;

namespace Salient.Features.Sift;

/// <summary>Builds the Gaussian scale space: s + 3 progressively blurred layers per octave (Lowe 2004, section 3).</summary>
internal static class GaussianPyramid
{
    /// <summary>Blur assumed to be present in the input image already (Lowe 2004, section 3.3).</summary>
    public const double AssumedInputBlur = 0.5;

    /// <summary>Octaves stop before their smaller side drops below this many pixels.</summary>
    private const int MinOctaveSize = 8;

    public static ScaleSpace Build(ImageF image, SiftParameters parameters, CancellationToken cancellationToken)
    {
        var s = parameters.LayersPerOctave;
        var sigma = parameters.Sigma;

        // The first layer must have blur σ₀. The input already carries AssumedInputBlur (doubled when upsampled),
        // so only the difference is added. Blurs compose in quadrature.
        ImageF baseImage;
        int firstIndex;
        double presentBlur;
        if (parameters.UpsampleInput)
        {
            baseImage = Resampling.ResizeBilinear(image, image.Width * 2, image.Height * 2);
            firstIndex = -1;
            presentBlur = 2 * AssumedInputBlur;
        }
        else
        {
            baseImage = image;
            firstIndex = 0;
            presentBlur = AssumedInputBlur;
        }

        var initialBlur = Math.Sqrt(Math.Max((sigma * sigma) - (presentBlur * presentBlur), 0.01));
        baseImage = GaussianBlur.Apply(baseImage, initialBlur);

        var increments = LayerIncrements(sigma, s);
        var octaveCount = OctaveCount(image, parameters);
        var octaves = new List<Octave>(octaveCount);

        for (var o = 0; o < octaveCount; o++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Layer s of the previous octave has σ = 2σ₀; halving it gives σ₀ in the new octave's pixels.
            var first = o == 0 ? baseImage : Resampling.Downsample2(octaves[o - 1].Gaussians[s]);
            if (Math.Min(first.Width, first.Height) < MinOctaveSize)
            {
                break;
            }

            var layers = new ImageF[s + 3];
            layers[0] = first;
            for (var i = 1; i < layers.Length; i++)
            {
                layers[i] = GaussianBlur.Apply(layers[i - 1], increments[i]);
            }

            octaves.Add(new Octave(firstIndex + o, layers));
        }

        return new ScaleSpace(octaves, s, sigma);
    }

    /// <summary>
    /// σ to add to layer i − 1 to obtain layer i: √((σ₀kⁱ)² − (σ₀kⁱ⁻¹)²) with k = 2^(1/s).
    /// Element 0 is σ₀ itself and is not applied.
    /// </summary>
    public static double[] LayerIncrements(double sigma, int layersPerOctave)
    {
        var k = Math.Pow(2, 1.0 / layersPerOctave);
        var increments = new double[layersPerOctave + 3];
        increments[0] = sigma;
        for (var i = 1; i < increments.Length; i++)
        {
            var previous = sigma * Math.Pow(k, i - 1);
            increments[i] = GaussianBlur.IncrementalSigma(previous, previous * k);
        }

        return increments;
    }

    /// <summary>
    /// Requested octave count, or the OpenCV rule round(log₂(min side of the first octave) − 2) − first octave number.
    /// Upsampling therefore adds two octaves: one for the doubled size and one for the extra octave −1.
    /// </summary>
    public static int OctaveCount(ImageF image, SiftParameters parameters)
    {
        if (parameters.Octaves > 0)
        {
            return parameters.Octaves;
        }

        var minSide = Math.Min(image.Width, image.Height);
        var count = parameters.UpsampleInput
            ? (int)Math.Round(Math.Log2(2.0 * minSide) - 2) + 1
            : (int)Math.Round(Math.Log2(minSide) - 2);
        return Math.Max(1, count);
    }
}
