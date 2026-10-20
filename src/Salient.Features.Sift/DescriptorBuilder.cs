using Salient.Core;

namespace Salient.Features.Sift;

/// <summary>Builds 128-dimensional SIFT descriptors (Lowe 2004, section 6).</summary>
internal static class DescriptorBuilder
{
    /// <summary>The descriptor window is a grid of Width × Width cells.</summary>
    public const int Width = 4;

    /// <summary>Orientation bins per cell.</summary>
    public const int Bins = 8;

    /// <summary>Values per descriptor: 4 × 4 × 8.</summary>
    public const int Dimension = Width * Width * Bins;

    /// <summary>One cell spans this many keypoint σ.</summary>
    public const double CellSizeFactor = 3.0;

    /// <summary>After normalization, values are clipped to this to reduce the influence of large gradients.</summary>
    public const float MagnitudeClip = 0.2f;

    /// <summary>Computes descriptors for all keypoints in parallel; row i belongs to keypoint i.</summary>
    public static float[] ComputeAll(ScaleSpace scaleSpace, IReadOnlyList<Keypoint> keypoints, CancellationToken cancellationToken)
    {
        var descriptors = new float[keypoints.Count * Dimension];
        Parallel.For(
            0,
            keypoints.Count,
            new ParallelOptions { CancellationToken = cancellationToken },
            i => Compute(scaleSpace, keypoints[i], descriptors.AsSpan(i * Dimension, Dimension)));
        return descriptors;
    }

    /// <summary>
    /// Accumulates gradient orientations, relative to the keypoint orientation, into a rotated grid of histograms with
    /// trilinear interpolation over (row, column, orientation) and a Gaussian weight of σ = half the window width.
    /// The result is normalized to unit length, clipped at <see cref="MagnitudeClip"/> and normalized again.
    /// </summary>
    public static void Compute(ScaleSpace scaleSpace, Keypoint keypoint, Span<float> descriptor)
    {
        var (octave, layer) = Locate(scaleSpace, keypoint);
        var image = octave.Gaussians[layer];
        var pixel = octave.PixelSize;
        var centerX = (int)Math.Round(keypoint.X / pixel);
        var centerY = (int)Math.Round(keypoint.Y / pixel);
        var sigma = keypoint.Scale / pixel;

        var cellWidth = CellSizeFactor * sigma;
        var radius = (int)Math.Round(cellWidth * Math.Sqrt(2) * (Width + 1) * 0.5);
        radius = Math.Min(radius, (int)Math.Sqrt((image.Width * image.Width) + (image.Height * image.Height)));
        var angle = keypoint.Angle * Math.PI / 180;
        var cos = Math.Cos(angle) / cellWidth;
        var sin = Math.Sin(angle) / cellWidth;
        var weightFactor = -1.0 / (Width * Width * 0.5);
        const double binsPerDegree = Bins / 360.0;

        // Histogram with a one-cell border on each side and one extra orientation bin, so interpolation needs no bounds checks.
        const int rowStride = (Width + 2) * (Bins + 2);
        var histogram = new double[(Width + 2) * rowStride];

        for (var dy = -radius; dy <= radius; dy++)
        {
            var y = centerY + dy;
            if (y <= 0 || y >= image.Height - 1)
            {
                continue;
            }

            for (var dx = -radius; dx <= radius; dx++)
            {
                var x = centerX + dx;
                if (x <= 0 || x >= image.Width - 1)
                {
                    continue;
                }

                // Sample position in the keypoint's rotated frame, in cells.
                var colRotated = (dx * cos) - (dy * sin);
                var rowRotated = (dx * sin) + (dy * cos);
                var rowBin = rowRotated + (Width / 2.0) - 0.5;
                var colBin = colRotated + (Width / 2.0) - 0.5;
                if (rowBin <= -1 || rowBin >= Width || colBin <= -1 || colBin >= Width)
                {
                    continue;
                }

                double gx = image[x + 1, y] - image[x - 1, y];
                double gy = image[x, y - 1] - image[x, y + 1];
                var magnitude = Math.Sqrt((gx * gx) + (gy * gy)) *
                    Math.Exp(((colRotated * colRotated) + (rowRotated * rowRotated)) * weightFactor);
                var gradientAngle = Math.Atan2(gy, gx) * (180 / Math.PI);
                if (gradientAngle < 0)
                {
                    gradientAngle += 360;
                }

                var orientationBin = (gradientAngle - keypoint.Angle) * binsPerDegree;
                Accumulate(histogram, rowBin, colBin, orientationBin, magnitude);
            }
        }

        for (var row = 0; row < Width; row++)
        {
            for (var col = 0; col < Width; col++)
            {
                var index = (((row + 1) * (Width + 2)) + col + 1) * (Bins + 2);

                // Fold the wrap-around bins back onto the first ones.
                histogram[index] += histogram[index + Bins];
                histogram[index + 1] += histogram[index + Bins + 1];
                for (var bin = 0; bin < Bins; bin++)
                {
                    descriptor[(((row * Width) + col) * Bins) + bin] = (float)histogram[index + bin];
                }
            }
        }

        Normalize(descriptor);
        var clipped = false;
        for (var i = 0; i < descriptor.Length; i++)
        {
            if (descriptor[i] > MagnitudeClip)
            {
                descriptor[i] = MagnitudeClip;
                clipped = true;
            }
        }

        if (clipped)
        {
            Normalize(descriptor);
        }
    }

    /// <summary>Spreads one sample over the 8 neighbouring (row, column, orientation) bins.</summary>
    private static void Accumulate(double[] histogram, double rowBin, double colBin, double orientationBin, double magnitude)
    {
        const int rowStride = (Width + 2) * (Bins + 2);
        const int colStride = Bins + 2;

        var r0 = (int)Math.Floor(rowBin);
        var c0 = (int)Math.Floor(colBin);
        var o0 = (int)Math.Floor(orientationBin);
        var fr = rowBin - r0;
        var fc = colBin - c0;
        var fo = orientationBin - o0;
        o0 = ((o0 % Bins) + Bins) % Bins;

        var index = ((r0 + 1) * rowStride) + ((c0 + 1) * colStride) + o0;
        for (var r = 0; r <= 1; r++)
        {
            var wr = r == 0 ? 1 - fr : fr;
            for (var c = 0; c <= 1; c++)
            {
                var wc = c == 0 ? 1 - fc : fc;
                for (var o = 0; o <= 1; o++)
                {
                    var wo = o == 0 ? 1 - fo : fo;
                    histogram[index + (r * rowStride) + (c * colStride) + o] += magnitude * wr * wc * wo;
                }
            }
        }
    }

    private static void Normalize(Span<float> values)
    {
        double sum = 0;
        foreach (var v in values)
        {
            sum += v * v;
        }

        var scale = (float)(1 / Math.Max(Math.Sqrt(sum), 1e-12));
        for (var i = 0; i < values.Length; i++)
        {
            values[i] *= scale;
        }
    }

    /// <summary>
    /// The octave and Gaussian layer a keypoint was detected in. Keypoints from other detectors carry no matching
    /// octave and layer, so they are derived from the scale and clamped to the pyramid.
    /// </summary>
    private static (Octave Octave, int Layer) Locate(ScaleSpace scaleSpace, Keypoint keypoint)
    {
        var s = scaleSpace.LayersPerOctave;
        foreach (var octave in scaleSpace.Octaves)
        {
            if (octave.Index == keypoint.Octave && keypoint.Layer >= 0 && keypoint.Layer < octave.Gaussians.Length)
            {
                return (octave, keypoint.Layer);
            }
        }

        var octaves = scaleSpace.Octaves;
        var scales = Math.Log2(Math.Max(keypoint.Scale, 1e-6) / scaleSpace.Sigma);
        var octaveNumber = Math.Clamp((int)Math.Floor(scales), octaves[0].Index, octaves[^1].Index);
        var chosen = octaves[octaveNumber - octaves[0].Index];
        var layer = Math.Clamp((int)Math.Round((scales - octaveNumber) * s), 0, chosen.Gaussians.Length - 1);
        return (chosen, layer);
    }
}
