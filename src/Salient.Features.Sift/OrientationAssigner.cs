using Salient.Core;

namespace Salient.Features.Sift;

/// <summary>Assigns one or more dominant gradient orientations to each keypoint (Lowe 2004, section 5).</summary>
internal static class OrientationAssigner
{
    /// <summary>Number of histogram bins (10° each).</summary>
    public const int Bins = 36;

    /// <summary>The weighting window's σ is this factor times the keypoint's scale.</summary>
    public const double WindowSigmaFactor = 1.5;

    /// <summary>The window radius is this many window σ.</summary>
    public const double WindowRadiusFactor = 3.0;

    /// <summary>Every local peak at least this fraction of the highest one produces a keypoint.</summary>
    public const double PeakRatio = 0.8;

    /// <summary>A keypoint with an orientation, and the histogram it came from.</summary>
    public readonly record struct OrientedKeypoint(RefinedPoint Source, Keypoint Keypoint, float[] Histogram);

    /// <summary>Assigns orientations to all points in parallel; results keep the input order.</summary>
    public static List<OrientedKeypoint> AssignAll(ScaleSpace scaleSpace, IReadOnlyList<RefinedPoint> points, CancellationToken cancellationToken)
    {
        var results = new List<OrientedKeypoint>?[points.Count];
        Parallel.For(
            0,
            points.Count,
            new ParallelOptions { CancellationToken = cancellationToken },
            i => results[i] = Assign(scaleSpace, points[i]));

        var all = new List<OrientedKeypoint>(points.Count);
        foreach (var result in results)
        {
            all.AddRange(result!);
        }

        return all;
    }

    /// <summary>Returns one keypoint per orientation peak; none when the neighbourhood has no gradient.</summary>
    public static List<OrientedKeypoint> Assign(ScaleSpace scaleSpace, RefinedPoint point)
    {
        var octave = scaleSpace.Octaves[point.Location.Octave];
        var fractionalLayer = point.Location.Layer + point.OffsetLayer;
        var octaveSigma = scaleSpace.Sigma * Math.Pow(2, fractionalLayer / scaleSpace.LayersPerOctave);

        var histogram = Histogram(octave.Gaussians[point.Location.Layer], point.Location.X, point.Location.Y, octaveSigma);
        var peak = histogram.Max();
        var keypoints = new List<OrientedKeypoint>(1);
        if (peak <= 0)
        {
            return keypoints;
        }

        for (var i = 0; i < histogram.Length; i++)
        {
            histogram[i] /= peak;
        }

        var x = (point.Location.X + point.OffsetX) * octave.PixelSize;
        var y = (point.Location.Y + point.OffsetY) * octave.PixelSize;
        var scale = scaleSpace.SigmaAt(octave, fractionalLayer);
        var response = MathF.Abs(point.Value);

        for (var bin = 0; bin < Bins; bin++)
        {
            var left = histogram[(bin + Bins - 1) % Bins];
            var right = histogram[(bin + 1) % Bins];
            var center = histogram[bin];
            if (center <= left || center <= right || center < PeakRatio)
            {
                continue;
            }

            // Fit a parabola through the peak and its neighbours to place the orientation between bin centers.
            var interpolated = bin + (0.5f * (left - right) / (left - (2 * center) + right));
            interpolated = (interpolated + Bins) % Bins;
            var angle = 360f / Bins * interpolated;
            if (angle >= 360f)
            {
                angle -= 360f;
            }

            keypoints.Add(new OrientedKeypoint(
                point,
                new Keypoint(x, y, scale, angle, response, octave.Index, point.Location.Layer),
                histogram));
        }

        return keypoints;
    }

    /// <summary>
    /// Smoothed 36-bin histogram of gradient directions around (x, y), weighted by gradient magnitude and a Gaussian window.
    /// Directions are counterclockwise as seen on screen (Y up), so the image row difference is negated.
    /// </summary>
    private static float[] Histogram(ImageF image, int cx, int cy, double octaveSigma)
    {
        var windowSigma = WindowSigmaFactor * octaveSigma;
        var radius = (int)Math.Round(WindowRadiusFactor * windowSigma);
        var weightFactor = -1.0 / (2 * windowSigma * windowSigma);
        var raw = new double[Bins];

        for (var dy = -radius; dy <= radius; dy++)
        {
            var y = cy + dy;
            if (y <= 0 || y >= image.Height - 1)
            {
                continue;
            }

            var row = image.Row(y);
            var above = image.Row(y - 1);
            var below = image.Row(y + 1);
            for (var dx = -radius; dx <= radius; dx++)
            {
                var x = cx + dx;
                if (x <= 0 || x >= image.Width - 1)
                {
                    continue;
                }

                double gx = row[x + 1] - row[x - 1];
                double gy = above[x] - below[x];
                var magnitude = Math.Sqrt((gx * gx) + (gy * gy));
                var angle = Math.Atan2(gy, gx) * (180 / Math.PI);
                var bin = (int)Math.Round(angle * Bins / 360);
                bin = ((bin % Bins) + Bins) % Bins;
                raw[bin] += magnitude * Math.Exp(((dx * dx) + (dy * dy)) * weightFactor);
            }
        }

        // Circular smoothing with the binomial kernel [1 4 6 4 1] / 16.
        var smoothed = new float[Bins];
        for (var i = 0; i < Bins; i++)
        {
            smoothed[i] = (float)(((raw[(i + Bins - 2) % Bins] + raw[(i + 2) % Bins]) / 16)
                + ((raw[(i + Bins - 1) % Bins] + raw[(i + 1) % Bins]) * 4 / 16)
                + (raw[i] * 6 / 16));
        }

        return smoothed;
    }
}
