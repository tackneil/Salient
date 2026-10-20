using Salient.Core;
using Salient.Features;
using Salient.Matching;

namespace Salient.Evaluation.Tests;

public sealed class RobustnessMetricsTests
{
    private const int Size = 200;

    [Fact]
    public void Metrics_count_repeated_and_correctly_matched_keypoints()
    {
        // Ten reference keypoints; the transformed image re-detects eight of them and adds two unrelated ones.
        // Descriptors are one-hot, so a re-detected keypoint matches exactly and the others fail the ratio test.
        var positions = Enumerable.Range(0, 10).Select(i => (X: 20f + (i * 15), Y: 30f + (i * 12))).ToList();
        var reference = OneHot(positions.Select((p, i) => (p.X, p.Y, Id: i)).ToList());
        var transformed = OneHot(
        [
            .. positions.Take(8).Select((p, i) => (p.X, p.Y, Id: i)),
            (150f, 20f, 10),
            (20f, 180f, 11),
        ]);

        var metrics = RobustnessMetrics.Compute(reference, transformed, Homography.Identity, Size, Size);

        Assert.Equal(0.8, metrics.Repeatability, 9);
        Assert.Equal(8, metrics.Matches);
        Assert.Equal(8, metrics.CorrectMatches);
        Assert.Equal(0.8, metrics.MatchingScore, 9);
        Assert.Equal(1.0, metrics.MatchPrecision, 9);
    }

    [Fact]
    public void Keypoints_at_an_inconsistent_scale_are_not_repeated()
    {
        var reference = OneHot([(50f, 50f, 0)], scale: 2);
        var transformed = OneHot([(50f, 50f, 0)], scale: 6);

        var metrics = RobustnessMetrics.Compute(reference, transformed, Homography.Identity, Size, Size);

        Assert.Equal(0, metrics.Repeatability);
    }

    [Fact]
    public void Only_keypoints_visible_in_both_images_count()
    {
        // Shift everything 150 px to the right: only reference points with x < 50 stay inside the transformed image.
        var shift = Homography.FromRowMajor([1, 0, 150, 0, 1, 0, 0, 0, 1]);
        var reference = OneHot([(10f, 100f, 0), (40f, 100f, 1), (120f, 100f, 2)]);
        var transformed = OneHot([(160f, 100f, 0), (190f, 100f, 1)]);

        var metrics = RobustnessMetrics.Compute(reference, transformed, shift, Size, Size);

        Assert.Equal(1.0, metrics.Repeatability, 9);
        Assert.Equal(1.0, metrics.MatchingScore, 9);
    }

    private static DescriptorSet OneHot(List<(float X, float Y, int Id)> points, float scale = 2)
    {
        const int dimension = 16;
        var values = new float[points.Count * dimension];
        for (var i = 0; i < points.Count; i++)
        {
            values[(i * dimension) + points[i].Id] = 1;
        }

        var keypoints = points.Select(p => new Keypoint(p.X, p.Y, scale, 0, 1, 0, 1)).ToList();
        return DescriptorSet.FromFloats(keypoints, dimension, values);
    }
}
