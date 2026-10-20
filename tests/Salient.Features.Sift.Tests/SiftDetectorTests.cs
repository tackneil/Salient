using System.ComponentModel.DataAnnotations;
using Salient.Features.Pipeline;

namespace Salient.Features.Sift.Tests;

public sealed class SiftDetectorTests
{
    private readonly SiftDetector _detector = new();

    [Fact]
    public void Single_blob_gives_one_keypoint_at_its_center_and_scale()
    {
        const double blobSigma = 6;
        var image = SyntheticImages.Blob(128, 64, 64, blobSigma);

        var keypoints = _detector.Detect(image, new SiftParameters()).Keypoints;

        // A rotationally symmetric blob has a nearly flat orientation histogram, so several orientations pass the 80% peak
        // rule. Each becomes a keypoint, but all of them come from the same detection: one position, one scale.
        var atBlob = keypoints.Where(k => Distance(k, 64, 64) < 3).ToList();
        Assert.NotEmpty(atBlob);
        Assert.Single(atBlob.Select(k => (k.X, k.Y, k.Scale)).Distinct());
        var blob = atBlob[0];
        Assert.InRange(blob.X, 63.5f, 64.5f);
        Assert.InRange(blob.Y, 63.5f, 64.5f);

        // The scale-normalized LoG of a Gaussian blob peaks at σ = σ_blob. DoG samples the Laplacian between σ and kσ, and
        // the reported σ is the lower one, so the expected value is σ_blob / √k, with k = 2^(1/s).
        var expected = blobSigma / Math.Pow(2, 0.5 / 3);
        Assert.InRange(blob.Scale / expected, 0.9, 1.1);
    }

    [Theory]
    [InlineData(64.3, 63.6)]
    [InlineData(63.75, 64.4)]
    public void Blob_is_localized_to_sub_pixel_accuracy(double cx, double cy)
    {
        var image = SyntheticImages.Blob(128, cx, cy, sigma: 5);

        var keypoints = _detector.Detect(image, new SiftParameters()).Keypoints;

        var blob = keypoints.MinBy(k => Distance(k, cx, cy));
        Assert.True(Distance(blob, cx, cy) < 0.25, $"Keypoint at ({blob.X}, {blob.Y}), blob at ({cx}, {cy}).");
    }

    [Fact]
    public void Perfectly_straight_edge_is_rejected_before_edge_rejection()
    {
        // Along a perfectly straight edge D is constant, so the 3×3 Hessian is singular and refinement discards the candidates.
        var image = SyntheticImages.Edge(96, edgeX: 48);
        var observer = new RecordingObserver(PipelineDetail.Progress);

        var keypoints = _detector.Detect(image, new SiftParameters(), observer: observer).Keypoints;

        Assert.Empty(keypoints);
        Assert.Equal(0, observer.Summaries[SiftStages.Refine].Kept);
    }

    [Fact]
    public void Gently_curved_edge_is_removed_by_edge_rejection()
    {
        // The boundary of a large disk is an edge with small but non-zero curvature, like edges in real images.
        const double radius = 50;
        var image = SyntheticImages.Disk(160, 80, 80, radius);
        var observer = new RecordingObserver(PipelineDetail.Progress);

        var keypoints = _detector.Detect(image, new SiftParameters(), observer: observer).Keypoints;

        Assert.DoesNotContain(keypoints, k => Math.Abs(Distance(k, 80, 80) - radius) < 6);
        Assert.True(observer.Summaries[SiftStages.Edge].Rejected > 0, "Candidates on the boundary should reach edge rejection and fail it.");
    }

    [Fact]
    public void Rotating_the_image_by_90_degrees_rotates_the_orientation_by_90_degrees()
    {
        // A large blob with a smaller one beside it: the gradient field is asymmetric, so the orientation is well defined.
        var image = SyntheticImages.Blob(129, 64, 64, sigma: 6);
        SyntheticImages.AddBlob(image, 71, 62, sigma: 2.5, amplitude: 0.35f);
        var rotated = SyntheticImages.RotateCounterclockwise90(image);

        var original = _detector.Detect(image, new SiftParameters()).Keypoints
            .Where(k => Distance(k, 64, 64) < 3).MaxBy(k => k.Response);
        var turned = _detector.Detect(rotated, new SiftParameters()).Keypoints
            .Where(k => Distance(k, 64, 64) < 3).MaxBy(k => k.Response);

        var difference = ((turned.Angle - original.Angle) % 360 + 360) % 360;
        Assert.InRange(difference, 87, 93);
    }

    [Fact]
    public void Diagnostics_reproduce_stricter_thresholds_exactly()
    {
        var image = SyntheticImages.Texture(160, 120, seed: 42);
        var strict = new SiftParameters { ContrastThreshold = 0.04, EdgeThreshold = 10 };

        var expected = _detector.Detect(image, strict).Keypoints;
        var permissive = _detector.Detect(image, strict.Permissive(), DetectionOptions.WithDiagnostics);

        var maxRatio = EdgeFilter.MaxRatio(strict.EdgeThreshold);
        var filtered = permissive.Keypoints
            .Where((_, i) => permissive.Diagnostics![i].Contrast >= (float)strict.ContrastThreshold
                && permissive.Diagnostics[i].EdgeRatio < maxRatio)
            .ToList();

        Assert.True(permissive.Keypoints.Count > expected.Count, "The permissive run should find more keypoints.");
        Assert.Equal(expected, filtered);
    }

    [Fact]
    public void Diagnostics_are_aligned_with_keypoints()
    {
        var image = SyntheticImages.Texture(96, 96, seed: 3);

        var result = _detector.Detect(image, new SiftParameters(), DetectionOptions.WithDiagnostics);

        Assert.NotNull(result.Diagnostics);
        Assert.Equal(result.Keypoints.Count, result.Diagnostics.Count);
        Assert.All(result.Diagnostics, d =>
        {
            Assert.Equal(OrientationAssigner.Bins, d.OrientationHistogram!.Count);
            Assert.Equal(1f, d.OrientationHistogram.Max());
            Assert.True(d.Contrast >= 0.04f);
        });
        Assert.Null(_detector.Detect(image, new SiftParameters()).Diagnostics);
    }

    [Fact]
    public void Progress_reports_every_stage_in_order_with_consistent_counts()
    {
        var image = SyntheticImages.Texture(96, 96, seed: 5);
        var observer = new RecordingObserver(PipelineDetail.Progress);

        var keypoints = _detector.Detect(image, new SiftParameters(), observer: observer).Keypoints;

        string[] expectedStages =
        [
            SiftStages.GaussianPyramid, SiftStages.DifferenceOfGaussians, SiftStages.Extrema,
            SiftStages.Refine, SiftStages.Contrast, SiftStages.Edge, SiftStages.Orientation,
        ];
        Assert.Equal(expectedStages, observer.Started);
        Assert.Equal(expectedStages, observer.Completed);
        Assert.Empty(observer.Artifacts);

        foreach (var stage in new[] { SiftStages.Refine, SiftStages.Contrast, SiftStages.Edge })
        {
            var summary = observer.Summaries[stage];
            Assert.Equal(summary.Input, summary.Kept + summary.Rejected);
        }

        Assert.Equal(observer.Summaries[SiftStages.Edge].Kept, observer.Summaries[SiftStages.Orientation].Input);
        Assert.Equal(keypoints.Count, observer.Summaries[SiftStages.Orientation].Kept);
    }

    [Fact]
    public void Full_detail_emits_layers_candidates_and_heatmaps_without_changing_the_result()
    {
        var image = SyntheticImages.Texture(96, 96, seed: 7);
        var parameters = new SiftParameters { Octaves = 2 };
        var observer = new RecordingObserver(PipelineDetail.Full);

        var withArtifacts = _detector.Detect(image, parameters, observer: observer).Keypoints;
        var plain = _detector.Detect(image, parameters).Keypoints;

        Assert.Equal(plain, withArtifacts);
        var layers = observer.Artifacts.OfType<ImageLayerArtifact>().ToList();
        Assert.Equal(2 * 6, layers.Count(l => l.Kind == ImageLayerKind.Gaussian));
        Assert.Equal(2 * 5, layers.Count(l => l.Kind == ImageLayerKind.DifferenceOfGaussians));
        Assert.Equal(4, observer.Artifacts.OfType<HeatmapArtifact>().Count());
        Assert.All(observer.Artifacts.OfType<HeatmapArtifact>(), h => Assert.Equal(96, h.Map.Width));

        var edgeCandidates = observer.ArtifactsOf<CandidateSetArtifact>(SiftStages.Edge).Single().Candidates;
        Assert.Equal(observer.Summaries[SiftStages.Edge].Rejected, edgeCandidates.Count(c => c.Rejection == RejectionReason.EdgeResponse));
    }

    [Fact]
    public void Cancellation_stops_detection()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            _detector.Detect(SyntheticImages.Texture(64, 64, seed: 1), new SiftParameters(), cancellationToken: cancellation.Token));
    }

    [Fact]
    public void Out_of_range_parameters_are_rejected()
    {
        Assert.Throws<ValidationException>(() =>
            _detector.Detect(SyntheticImages.Texture(64, 64, seed: 1), new SiftParameters { LayersPerOctave = 0 }));
    }

    private static double Distance(Keypoint k, double x, double y) =>
        Math.Sqrt(((k.X - x) * (k.X - x)) + ((k.Y - y) * (k.Y - y)));
}
