using Salient.Core;
using Salient.Features;

namespace Salient.Evaluation;

/// <summary>
/// A detector + descriptor under test. Supplied by the caller, so this library stays independent of concrete algorithms
/// (ADR-0002).
/// </summary>
/// <param name="Id">Stable identifier, for example <c>sift</c>.</param>
/// <param name="DisplayName">Name shown in the UI.</param>
/// <param name="DetectAndCompute">Detects keypoints and computes their descriptors; the set carries the keypoints.</param>
public sealed record FeatureExtractor(string Id, string DisplayName, Func<ImageF, CancellationToken, DescriptorSet> DetectAndCompute);

/// <summary>One measured point of a curve.</summary>
/// <param name="Value">Transformation value, for example the angle.</param>
/// <param name="Metrics">Metrics at that value.</param>
public sealed record SweepPoint(double Value, PairMetrics Metrics);

/// <summary>The curve of one extractor over one family.</summary>
/// <param name="ExtractorId">Extractor identifier.</param>
/// <param name="DisplayName">Extractor display name.</param>
/// <param name="Points">Measurements in the order of the swept values.</param>
public sealed record SweepSeries(string ExtractorId, string DisplayName, IReadOnlyList<SweepPoint> Points);

/// <summary>All curves for one transformation family.</summary>
/// <param name="Family">Swept family.</param>
/// <param name="Values">Swept values.</param>
/// <param name="Series">One series per extractor.</param>
public sealed record SweepResult(TransformationFamily Family, IReadOnlyList<double> Values, IReadOnlyList<SweepSeries> Series);

/// <summary>Progress of a running sweep.</summary>
/// <param name="Completed">Finished measurements.</param>
/// <param name="Total">All measurements.</param>
/// <param name="Family">Family being measured.</param>
/// <param name="ExtractorId">Extractor being measured.</param>
/// <param name="Value">Value just measured.</param>
public sealed record SweepProgress(int Completed, int Total, TransformationFamily Family, string ExtractorId, double Value);

/// <summary>Runs robustness sweeps: families × values × extractors.</summary>
public static class RobustnessSweep
{
    /// <summary>
    /// Measures every extractor on every value of every family. The reference image is described once per extractor;
    /// each transformed image is generated once per value and shared by all extractors.
    /// </summary>
    /// <param name="reference">Reference image.</param>
    /// <param name="families">Families with the values to sweep; see <see cref="Transformations.DefaultValues"/>.</param>
    /// <param name="extractors">Extractors to compare.</param>
    /// <param name="options">Metric tolerances.</param>
    /// <param name="progress">Receives one report per measurement.</param>
    /// <param name="cancellationToken">Cancels between measurements.</param>
    public static IReadOnlyList<SweepResult> Run(
        ImageF reference,
        IReadOnlyDictionary<TransformationFamily, double[]> families,
        IReadOnlyList<FeatureExtractor> extractors,
        MetricOptions? options = null,
        IProgress<SweepProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(families);
        ArgumentNullException.ThrowIfNull(extractors);

        var total = families.Values.Sum(v => v.Length) * extractors.Count;
        var completed = 0;
        var referenceFeatures = extractors.ToDictionary(e => e.Id, e => e.DetectAndCompute(reference, cancellationToken));
        var results = new List<SweepResult>(families.Count);

        foreach (var (family, values) in families)
        {
            var points = extractors.ToDictionary(e => e.Id, _ => new List<SweepPoint>(values.Length));
            foreach (var value in values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var transformed = Transformations.Apply(reference, family, value);
                foreach (var extractor in extractors)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var features = extractor.DetectAndCompute(transformed.Image, cancellationToken);
                    var metrics = RobustnessMetrics.Compute(
                        referenceFeatures[extractor.Id], features, transformed.GroundTruth, reference.Width, reference.Height, options);
                    points[extractor.Id].Add(new SweepPoint(value, metrics));
                    progress?.Report(new SweepProgress(++completed, total, family, extractor.Id, value));
                }
            }

            results.Add(new SweepResult(
                family,
                values,
                extractors.Select(e => new SweepSeries(e.Id, e.DisplayName, points[e.Id])).ToList()));
        }

        return results;
    }
}
