using Salient.Core;
using Salient.Matching;

namespace Salient.Evaluation;

/// <summary>Geometric warping of images by a homography.</summary>
public static class ImageWarp
{
    /// <summary>
    /// Warps <paramref name="source"/> so that a source point p appears at <paramref name="map"/>(p) in the result,
    /// sampling bilinearly. Pixels that map from outside the source are 0.
    /// </summary>
    public static ImageF Warp(ImageF source, Homography map, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(map);
        var inverse = map.Inverse() ?? throw new ArgumentException("The homography is not invertible.", nameof(map));
        var result = new ImageF(width, height);
        var (maxX, maxY) = (source.Width - 1, source.Height - 1);

        Parallel.For(0, height, y =>
        {
            var row = result.Row(y);
            for (var x = 0; x < width; x++)
            {
                var p = inverse.Map(new Point2(x, y));
                if (p.X >= 0 && p.X <= maxX && p.Y >= 0 && p.Y <= maxY)
                {
                    row[x] = source.SampleBilinear((float)p.X, (float)p.Y);
                }
            }
        });

        return result;
    }
}
