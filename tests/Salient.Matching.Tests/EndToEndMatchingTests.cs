using Salient.Core;
using Salient.Features.Sift;
using Salient.Features.Sift.Tests;
using Xunit.Abstractions;

namespace Salient.Matching.Tests;

public sealed class EndToEndMatchingTests(ITestOutputHelper output)
{
    [Fact]
    public void Sift_matching_and_ransac_recover_rotation_and_scale()
    {
        // The pair used by the design mockup: rotated by 10° and scaled by 0.85 around the center.
        var scene = GoldenScene.Create(480, 360, seed: 2026);
        var (warped, _) = GoldenScene.RotateAndScale(scene, degrees: 10, scale: 0.85);
        var detector = new SiftDetector();
        var parameters = new SiftParameters();

        var (_, a) = detector.DetectAndCompute(scene, parameters);
        var (_, b) = detector.DetectAndCompute(warped, parameters);
        var matches = MatchFilters.RatioTest(BruteForceMatcher.NearestTwo(a, b), ratio: 0.8);
        var from = matches.Select(m => new Point2(a.Keypoints[m.QueryIndex].X, a.Keypoints[m.QueryIndex].Y)).ToList();
        var to = matches.Select(m => new Point2(b.Keypoints[m.TrainIndex].X, b.Keypoints[m.TrainIndex].Y)).ToList();

        var estimate = RansacHomography.Estimate(from, to)!;

        output.WriteLine($"{matches.Count} matches, {estimate.InlierCount} inliers, {estimate.Iterations} iterations; " +
            $"rotation {estimate.Homography.RotationDegrees:F3}°, scale {estimate.Homography.Scale:F4}");
        Assert.InRange(estimate.Homography.RotationDegrees, 9.8, 10.2);
        Assert.InRange(estimate.Homography.Scale, 0.85 * 0.995, 0.85 * 1.005);
        Assert.True(estimate.InlierCount >= 0.8 * matches.Count, "Most ratio-test matches should be inliers.");
    }
}
