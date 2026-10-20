using Salient.Core;
using Salient.Features;
using Salient.Matching;

namespace Salient.Evaluation;

/// <summary>Measurements for one reference/transformed image pair.</summary>
/// <param name="Repeatability">
/// Fraction of keypoints, among those visible in both images, that are re-detected: within the position tolerance after
/// mapping and at a consistent scale. Divided by the smaller visible count, so it lies in [0, 1].
/// </param>
/// <param name="MatchingScore">Correct ratio-test matches divided by the reference keypoints visible in the transformed image.</param>
/// <param name="MatchPrecision">Correct matches divided by all ratio-test matches; 0 when there are no matches.</param>
/// <param name="ReferenceKeypoints">Keypoints in the reference image.</param>
/// <param name="TransformedKeypoints">Keypoints in the transformed image.</param>
/// <param name="Matches">Matches after the ratio test.</param>
/// <param name="CorrectMatches">Matches whose keypoints agree with the ground truth within the position tolerance.</param>
public sealed record PairMetrics(
    double Repeatability,
    double MatchingScore,
    double MatchPrecision,
    int ReferenceKeypoints,
    int TransformedKeypoints,
    int Matches,
    int CorrectMatches);

/// <summary>Options for <see cref="RobustnessMetrics"/>.</summary>
public sealed record MetricOptions
{
    /// <summary>Maximum distance in pixels between a mapped keypoint and its counterpart.</summary>
    public double PositionTolerance { get; init; } = 3.0;

    /// <summary>Maximum factor between the expected and the detected scale for a repeated keypoint.</summary>
    public double ScaleTolerance { get; init; } = 1.5;

    /// <summary>Ratio for Lowe's test when matching descriptors.</summary>
    public double MatchRatio { get; init; } = 0.8;
}

/// <summary>Repeatability and matching metrics in the style of Mikolajczyk and Schmid (2005), simplified to point distances.</summary>
public static class RobustnessMetrics
{
    /// <summary>Computes all metrics for a pair described by <paramref name="reference"/> and <paramref name="transformed"/>.</summary>
    /// <param name="reference">Keypoints and descriptors of the reference image.</param>
    /// <param name="transformed">Keypoints and descriptors of the transformed image.</param>
    /// <param name="groundTruth">Maps reference coordinates to transformed coordinates.</param>
    /// <param name="width">Image width (both images share it).</param>
    /// <param name="height">Image height.</param>
    /// <param name="options">Tolerances; defaults when <see langword="null"/>.</param>
    public static PairMetrics Compute(
        DescriptorSet reference,
        DescriptorSet transformed,
        Homography groundTruth,
        int width,
        int height,
        MetricOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(transformed);
        ArgumentNullException.ThrowIfNull(groundTruth);
        options ??= new MetricOptions();
        var inverse = groundTruth.Inverse() ?? throw new ArgumentException("The ground truth is not invertible.", nameof(groundTruth));
        var scaleFactor = groundTruth.Scale;
        var tolerance = options.PositionTolerance;

        // Reference keypoints that land inside the transformed image, and transformed keypoints that come from inside the reference.
        var mappedReference = reference.Keypoints.Select(k => groundTruth.Map(new Point2(k.X, k.Y))).ToArray();
        var visibleReference = Enumerable.Range(0, reference.Count).Where(i => Inside(mappedReference[i], width, height)).ToList();
        var visibleTransformed = Enumerable.Range(0, transformed.Count)
            .Where(j => Inside(inverse.Map(new Point2(transformed.Keypoints[j].X, transformed.Keypoints[j].Y)), width, height))
            .ToList();

        var index = new SpatialIndex(transformed.Keypoints, visibleTransformed, tolerance);
        var repeated = visibleReference.Count(i =>
        {
            var expectedScale = reference.Keypoints[i].Scale * scaleFactor;
            return index.Any(mappedReference[i], j =>
            {
                var ratio = transformed.Keypoints[j].Scale / expectedScale;
                return ratio < options.ScaleTolerance && ratio > 1 / options.ScaleTolerance;
            });
        });
        var denominator = Math.Min(visibleReference.Count, visibleTransformed.Count);
        var repeatability = denominator == 0 ? 0 : Math.Min(1, repeated / (double)denominator);

        var matches = reference.Count == 0 || transformed.Count == 0
            ? []
            : MatchFilters.RatioTest(BruteForceMatcher.NearestTwo(reference, transformed), options.MatchRatio);
        var correct = matches.Count(m =>
        {
            var b = transformed.Keypoints[m.TrainIndex];
            return mappedReference[m.QueryIndex].DistanceTo(new Point2(b.X, b.Y)) < tolerance;
        });

        return new PairMetrics(
            repeatability,
            visibleReference.Count == 0 ? 0 : Math.Min(1, correct / (double)visibleReference.Count),
            matches.Count == 0 ? 0 : correct / (double)matches.Count,
            reference.Count,
            transformed.Count,
            matches.Count,
            correct);
    }

    private static bool Inside(Point2 p, int width, int height) =>
        p.X >= 0 && p.X <= width - 1 && p.Y >= 0 && p.Y <= height - 1;

    /// <summary>A uniform grid over keypoint positions for radius queries.</summary>
    private sealed class SpatialIndex
    {
        private readonly Dictionary<(int, int), List<int>> _cells = [];
        private readonly IReadOnlyList<Keypoint> _points;
        private readonly double _radius;

        public SpatialIndex(IReadOnlyList<Keypoint> points, IEnumerable<int> indices, double radius)
        {
            _points = points;
            _radius = radius;
            foreach (var i in indices)
            {
                var key = Cell(points[i].X, points[i].Y);
                if (!_cells.TryGetValue(key, out var list))
                {
                    _cells[key] = list = [];
                }

                list.Add(i);
            }
        }

        public bool Any(Point2 center, Func<int, bool> predicate)
        {
            var (cx, cy) = Cell(center.X, center.Y);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (!_cells.TryGetValue((cx + dx, cy + dy), out var list))
                    {
                        continue;
                    }

                    foreach (var j in list)
                    {
                        if (center.DistanceTo(new Point2(_points[j].X, _points[j].Y)) < _radius && predicate(j))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private (int, int) Cell(double x, double y) => ((int)Math.Floor(x / _radius), (int)Math.Floor(y / _radius));
    }
}
