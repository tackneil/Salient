using Salient.Core;

namespace Salient.Matching;

/// <summary>Options for <see cref="RansacHomography"/>.</summary>
public sealed record RansacOptions
{
    /// <summary>Maximum reprojection error, in pixels, for a correspondence to count as an inlier.</summary>
    public double Threshold { get; init; } = 3.0;

    /// <summary>Desired probability that at least one sample is outlier-free; drives the adaptive iteration count.</summary>
    public double Confidence { get; init; } = 0.995;

    /// <summary>Upper bound on the number of samples.</summary>
    public int MaxIterations { get; init; } = 2000;

    /// <summary>Seed of the sampler; the same input and seed always give the same result (ADR-0009).</summary>
    public int Seed { get; init; } = 1;
}

/// <summary>A homography estimated by RANSAC.</summary>
/// <param name="Homography">The model refit on all inliers.</param>
/// <param name="Inliers">Per correspondence, whether it agrees with the model within the threshold.</param>
/// <param name="InlierCount">Number of inliers.</param>
/// <param name="Iterations">Samples drawn.</param>
public sealed record HomographyEstimate(Homography Homography, IReadOnlyList<bool> Inliers, int InlierCount, int Iterations);

/// <summary>Robust homography estimation with RANSAC (Fischler and Bolles, 1981).</summary>
public static class RansacHomography
{
    private const int SampleSize = 4;

    /// <summary>
    /// Repeatedly fits a homography to 4 random correspondences and keeps the model with the most inliers (ties go to the
    /// lower total error). The iteration count adapts to the best inlier ratio found so far. The winner is refit on its
    /// inliers until the inlier set stops growing. Returns <see langword="null"/> when no model can be fit.
    /// </summary>
    public static HomographyEstimate? Estimate(
        IReadOnlyList<Point2> from,
        IReadOnlyList<Point2> to,
        RansacOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (from.Count != to.Count)
        {
            throw new ArgumentException("Point lists must have the same length.", nameof(to));
        }

        options ??= new RansacOptions();
        var n = from.Count;
        if (n < SampleSize)
        {
            return null;
        }

        var random = new Random(options.Seed);
        var thresholdSquared = options.Threshold * options.Threshold;
        var sample = new int[SampleSize];
        var sampleFrom = new Point2[SampleSize];
        var sampleTo = new Point2[SampleSize];

        Homography? best = null;
        var (bestCount, bestError) = (0, double.MaxValue);
        var required = options.MaxIterations;
        var iterations = 0;

        while (iterations < required)
        {
            cancellationToken.ThrowIfCancellationRequested();
            iterations++;
            DrawSample(random, n, sample);
            for (var i = 0; i < SampleSize; i++)
            {
                (sampleFrom[i], sampleTo[i]) = (from[sample[i]], to[sample[i]]);
            }

            if (IsDegenerate(sampleFrom) || IsDegenerate(sampleTo))
            {
                continue;
            }

            var model = Homography.Estimate(sampleFrom, sampleTo);
            if (model is null)
            {
                continue;
            }

            var (count, error) = Score(model, from, to, thresholdSquared);
            if (count > bestCount || (count == bestCount && error < bestError))
            {
                (best, bestCount, bestError) = (model, count, error);
                required = Math.Min(required, AdaptiveIterations(bestCount / (double)n, options.Confidence));
            }
        }

        if (best is null)
        {
            return null;
        }

        var inliers = Classify(best, from, to, thresholdSquared);
        for (var refit = 0; refit < 5; refit++)
        {
            var subsetFrom = from.Where((_, i) => inliers[i]).ToList();
            var subsetTo = to.Where((_, i) => inliers[i]).ToList();
            var refined = Homography.Estimate(subsetFrom, subsetTo);
            if (refined is null)
            {
                break;
            }

            var refinedInliers = Classify(refined, from, to, thresholdSquared);
            if (refinedInliers.Count(x => x) < inliers.Count(x => x))
            {
                break;
            }

            var unchanged = refinedInliers.SequenceEqual(inliers);
            (best, inliers) = (refined, refinedInliers);
            if (unchanged)
            {
                break;
            }
        }

        return new HomographyEstimate(best, inliers, inliers.Count(x => x), iterations);
    }

    /// <summary>Samples needed so that, with inlier ratio w, one 4-point sample is outlier-free with probability p.</summary>
    internal static int AdaptiveIterations(double inlierRatio, double confidence)
    {
        var allInliers = Math.Pow(inlierRatio, SampleSize);
        if (allInliers >= 1)
        {
            return 1;
        }

        if (allInliers <= 0)
        {
            return int.MaxValue;
        }

        return (int)Math.Ceiling(Math.Log(1 - confidence) / Math.Log(1 - allInliers));
    }

    private static void DrawSample(Random random, int n, int[] sample)
    {
        for (var i = 0; i < sample.Length; i++)
        {
            int candidate;
            do
            {
                candidate = random.Next(n);
            }
            while (Array.IndexOf(sample, candidate, 0, i) >= 0);

            sample[i] = candidate;
        }
    }

    /// <summary>True when any three of the four points are (nearly) collinear, which makes the homography ill-defined.</summary>
    private static bool IsDegenerate(Point2[] p)
    {
        for (var i = 0; i < 4; i++)
        {
            for (var j = i + 1; j < 4; j++)
            {
                for (var k = j + 1; k < 4; k++)
                {
                    var area = ((p[j].X - p[i].X) * (p[k].Y - p[i].Y)) - ((p[j].Y - p[i].Y) * (p[k].X - p[i].X));
                    if (Math.Abs(area) < 1e-6)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static (int Count, double Error) Score(Homography model, IReadOnlyList<Point2> from, IReadOnlyList<Point2> to, double thresholdSquared)
    {
        var (count, error) = (0, 0.0);
        for (var i = 0; i < from.Count; i++)
        {
            var e = SquaredError(model, from[i], to[i]);
            if (e < thresholdSquared)
            {
                count++;
                error += e;
            }
        }

        return (count, error);
    }

    private static bool[] Classify(Homography model, IReadOnlyList<Point2> from, IReadOnlyList<Point2> to, double thresholdSquared)
    {
        var inliers = new bool[from.Count];
        for (var i = 0; i < from.Count; i++)
        {
            inliers[i] = SquaredError(model, from[i], to[i]) < thresholdSquared;
        }

        return inliers;
    }

    private static double SquaredError(Homography model, Point2 from, Point2 to)
    {
        var mapped = model.Map(from);
        var (dx, dy) = (mapped.X - to.X, mapped.Y - to.Y);
        var e = (dx * dx) + (dy * dy);
        return double.IsNaN(e) ? double.MaxValue : e;
    }
}
