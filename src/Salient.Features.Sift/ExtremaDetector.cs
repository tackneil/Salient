using Salient.Core;

namespace Salient.Features.Sift;

/// <summary>Finds local extrema of D over space and scale (Lowe 2004, section 3.1).</summary>
internal static class ExtremaDetector
{
    /// <summary>Pixels this close to the octave border are not searched; refinement and orientation need room.</summary>
    public const int ImageBorder = 5;

    /// <summary>
    /// Returns every DoG pixel in layers 1..s that is a maximum (positive) or minimum (negative) of its 26 neighbours
    /// and whose magnitude exceeds 0.5 · <paramref name="prefilterContrast"/> / s, a cheap prefilter as in OpenCV.
    /// Ties count as extrema, as in OpenCV. Results are ordered by octave, layer, row and column.
    /// </summary>
    /// <remarks>
    /// <see cref="SiftDetector"/> always passes <see cref="SiftParameters.MinContrastThreshold"/>, not the user's threshold, so the
    /// candidate set does not depend on the contrast threshold. Filtering a permissive run on the client then gives exactly
    /// the keypoints of a run with the stricter threshold (ADR-0003). OpenCV prefilters with the user's threshold instead.
    /// </remarks>
    public static List<ScaleSpacePoint> Find(ScaleSpace scaleSpace, double prefilterContrast, CancellationToken cancellationToken)
    {
        var s = scaleSpace.LayersPerOctave;
        var threshold = (float)(0.5 * prefilterContrast / s);
        var result = new List<ScaleSpacePoint>();

        for (var o = 0; o < scaleSpace.Octaves.Count; o++)
        {
            var octave = scaleSpace.Octaves[o];
            var width = octave.Width;
            var height = octave.Height;
            if (width <= 2 * ImageBorder || height <= 2 * ImageBorder)
            {
                continue;
            }

            for (var layer = 1; layer <= s; layer++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var below = octave.Dogs[layer - 1];
                var current = octave.Dogs[layer];
                var above = octave.Dogs[layer + 1];
                var rows = new List<ScaleSpacePoint>?[height];
                var octaveIndex = o;
                var layerIndex = layer;

                Parallel.For(ImageBorder, height - ImageBorder, y =>
                {
                    List<ScaleSpacePoint>? found = null;
                    for (var x = ImageBorder; x < width - ImageBorder; x++)
                    {
                        var value = current[x, y];
                        if (MathF.Abs(value) > threshold && IsExtremum(value, below, current, above, x, y))
                        {
                            (found ??= []).Add(new ScaleSpacePoint(octaveIndex, layerIndex, x, y));
                        }
                    }

                    rows[y] = found;
                });

                foreach (var row in rows)
                {
                    if (row is not null)
                    {
                        result.AddRange(row);
                    }
                }
            }
        }

        return result;
    }

    private static bool IsExtremum(float value, ImageF below, ImageF current, ImageF above, int x, int y)
    {
        if (value > 0)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                var b = below.Row(y + dy);
                var c = current.Row(y + dy);
                var a = above.Row(y + dy);
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (value < b[x + dx] || value < c[x + dx] || value < a[x + dx])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        for (var dy = -1; dy <= 1; dy++)
        {
            var b = below.Row(y + dy);
            var c = current.Row(y + dy);
            var a = above.Row(y + dy);
            for (var dx = -1; dx <= 1; dx++)
            {
                if (value > b[x + dx] || value > c[x + dx] || value > a[x + dx])
                {
                    return false;
                }
            }
        }

        return true;
    }
}
