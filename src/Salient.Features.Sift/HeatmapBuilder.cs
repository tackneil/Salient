using Salient.Core;
using Salient.Features.Pipeline;

namespace Salient.Features.Sift;

/// <summary>Builds the per-pixel maps shown as heatmaps in the Detector view (ADR-0004).</summary>
internal static class HeatmapBuilder
{
    /// <summary>
    /// Builds the DoG response, gradient magnitude, contrast and edge-ratio maps from the octave at input resolution
    /// (octave number 0) and its DoG layer 1, whose σ is σ₀·k. Returns nothing when that octave does not exist.
    /// </summary>
    public static IReadOnlyList<HeatmapArtifact> Build(ScaleSpace scaleSpace)
    {
        var octave = scaleSpace.Octaves.FirstOrDefault(o => o.Index == 0);
        if (octave is null || octave.Dogs.Length < 2)
        {
            return [];
        }

        var sigma = scaleSpace.SigmaAt(octave, 1);
        var dog = octave.Dogs[1];
        var s = scaleSpace.LayersPerOctave;

        var contrast = new ImageF(dog.Width, dog.Height);
        var dogPixels = dog.Pixels;
        var contrastPixels = contrast.Pixels;
        for (var i = 0; i < dogPixels.Length; i++)
        {
            contrastPixels[i] = MathF.Abs(dogPixels[i]) * s;
        }

        return
        [
            new HeatmapArtifact(HeatmapKind.DogResponse, sigma, dog.Clone()),
            new HeatmapArtifact(HeatmapKind.GradientMagnitude, sigma, GradientMagnitude(octave.Gaussians[1])),
            new HeatmapArtifact(HeatmapKind.Contrast, sigma, contrast),
            new HeatmapArtifact(HeatmapKind.EdgeRatio, sigma, EdgeRatio(dog)),
        ];
    }

    private static ImageF GradientMagnitude(ImageF image)
    {
        var (dx, dy) = ImageMath.Gradients(image);
        var result = new ImageF(image.Width, image.Height);
        var gx = dx.Pixels;
        var gy = dy.Pixels;
        var output = result.Pixels;
        for (var i = 0; i < output.Length; i++)
        {
            output[i] = MathF.Sqrt((gx[i] * gx[i]) + (gy[i] * gy[i]));
        }

        return result;
    }

    private static ImageF EdgeRatio(ImageF dog)
    {
        var width = dog.Width;
        var height = dog.Height;
        var result = new ImageF(width, height);

        Parallel.For(0, height, y =>
        {
            var row = result.Row(y);
            var ym = Border.Reflect101(y - 1, height);
            var yp = Border.Reflect101(y + 1, height);
            for (var x = 0; x < width; x++)
            {
                var xm = Border.Reflect101(x - 1, width);
                var xp = Border.Reflect101(x + 1, width);
                double v = dog[x, y];
                var dxx = dog[xp, y] + dog[xm, y] - (2 * v);
                var dyy = dog[x, yp] + dog[x, ym] - (2 * v);
                var dxy = 0.25 * (dog[xp, yp] - dog[xm, yp] - dog[xp, ym] + dog[xm, ym]);
                row[x] = EdgeFilter.RatioFromHessian(dxx, dyy, dxy);
            }
        });

        return result;
    }
}
