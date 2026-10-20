using Salient.Features.Sift;
using Salient.Features.Sift.Tests;

namespace Salient.Evaluation.Tests;

public sealed class RobustnessSweepTests
{
    private static readonly SiftDetector Sift = new();
    private static readonly FeatureExtractor SiftExtractor =
        new("sift", "SIFT · Salient", (image, ct) => Sift.DetectAndCompute(image, new SiftParameters(), cancellationToken: ct).Descriptors);

    [Fact]
    public void Identity_transformation_gives_perfect_scores()
    {
        var scene = GoldenScene.Create(240, 180, seed: 8);
        var families = new Dictionary<TransformationFamily, double[]> { [TransformationFamily.Rotation] = [0] };

        var result = Assert.Single(RobustnessSweep.Run(scene, families, [SiftExtractor]));

        var metrics = Assert.Single(Assert.Single(result.Series).Points).Metrics;
        Assert.True(metrics.Repeatability > 0.99, $"Repeatability {metrics.Repeatability:P1}.");
        Assert.True(metrics.MatchPrecision > 0.99, $"Precision {metrics.MatchPrecision:P1}.");
        Assert.True(metrics.MatchingScore > 0.8, $"Matching score {metrics.MatchingScore:P1}.");
    }

    [Fact]
    public void Sift_degrades_gracefully_under_rotation_and_blur()
    {
        var scene = GoldenScene.Create(240, 180, seed: 8);
        var families = new Dictionary<TransformationFamily, double[]>
        {
            [TransformationFamily.Rotation] = [0, 45],
            [TransformationFamily.Blur] = [0, 3],
        };

        var results = RobustnessSweep.Run(scene, families, [SiftExtractor]);

        foreach (var result in results)
        {
            var points = result.Series[0].Points;
            Assert.True(points[1].Metrics.MatchingScore < points[0].Metrics.MatchingScore, $"{result.Family}: no degradation.");
            Assert.True(points[1].Metrics.MatchPrecision > 0.8, $"{result.Family}: precision {points[1].Metrics.MatchPrecision:P1}.");
        }
    }

    [Fact]
    public void Progress_is_reported_per_measurement_and_cancellation_stops_the_sweep()
    {
        var scene = GoldenScene.Create(120, 90, seed: 8);
        var families = new Dictionary<TransformationFamily, double[]>
        {
            [TransformationFamily.Brightness] = [0, 20, 40],
            [TransformationFamily.Noise] = [0, 10],
        };
        var reports = new List<SweepProgress>();
        var progress = new SynchronousProgress<SweepProgress>(reports.Add);

        RobustnessSweep.Run(scene, families, [SiftExtractor, SiftExtractor with { Id = "sift-copy" }], progress: progress);

        Assert.Equal(10, reports.Count);
        Assert.Equal(Enumerable.Range(1, 10), reports.Select(r => r.Completed));
        Assert.All(reports, r => Assert.Equal(10, r.Total));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            RobustnessSweep.Run(scene, families, [SiftExtractor], cancellationToken: cancellation.Token));
    }

    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
