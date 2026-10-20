using Salient.Core;

namespace Salient.Matching.Tests;

public sealed class HomographyTests
{
    private static readonly Homography Projective = Homography.FromRowMajor([0.9, -0.15, 30, 0.2, 1.05, -12, 0.0004, -0.0002, 1]);

    [Fact]
    public void Similarity_reports_its_rotation_and_scale()
    {
        var h = Homography.Similarity(12.5, 0.8, new Point2(100, 50));

        Assert.Equal(12.5, h.RotationDegrees, 9);
        Assert.Equal(0.8, h.Scale, 9);
        var center = h.Map(new Point2(100, 50));
        Assert.Equal(100, center.X, 9);
        Assert.Equal(50, center.Y, 9);

        // Counterclockwise on screen with Y down: a point to the right of the center moves up.
        var right = h.Map(new Point2(110, 50));
        Assert.True(right.Y < 50);
    }

    [Fact]
    public void Inverse_undoes_the_mapping()
    {
        var inverse = Projective.Inverse()!;

        var p = new Point2(123.4, 56.7);
        var back = inverse.Map(Projective.Map(p));

        Assert.Equal(p.X, back.X, 8);
        Assert.Equal(p.Y, back.Y, 8);
        Assert.Null(Homography.FromRowMajor([1, 2, 3, 2, 4, 6, 0, 0, 1]).Inverse());
    }

    [Fact]
    public void DLT_recovers_a_homography_from_four_points()
    {
        Point2[] from = [new(0, 0), new(200, 10), new(190, 150), new(5, 140)];
        var to = from.Select(Projective.Map).ToList();

        var estimate = Homography.Estimate(from, to)!;

        AssertClose(Projective, estimate, 1e-6);
    }

    [Fact]
    public void DLT_is_accurate_with_noisy_points()
    {
        var random = new Random(3);
        var from = Enumerable.Range(0, 200).Select(_ => new Point2(random.NextDouble() * 640, random.NextDouble() * 480)).ToList();
        var to = from.Select(p => Projective.Map(p)).Select(p => new Point2(p.X + Noise(random, 0.5), p.Y + Noise(random, 0.5))).ToList();

        var estimate = Homography.Estimate(from, to)!;

        var worst = from.Max(p => estimate.Map(p).DistanceTo(Projective.Map(p)));
        Assert.True(worst < 0.5, $"Worst reprojection difference {worst:F3} px.");
    }

    [Fact]
    public void DLT_rejects_degenerate_input()
    {
        Point2[] collinearToo = [new(0, 0), new(0, 0), new(0, 0), new(0, 0)];

        Assert.Null(Homography.Estimate(collinearToo, collinearToo));
        Assert.Null(Homography.Estimate([new(0, 0)], [new(0, 0)]));
    }

    [Fact]
    public void Ransac_finds_the_model_and_the_outliers()
    {
        var random = new Random(11);
        const int count = 300;
        var from = new List<Point2>();
        var to = new List<Point2>();
        var isOutlier = new bool[count];
        for (var i = 0; i < count; i++)
        {
            var p = new Point2(random.NextDouble() * 640, random.NextDouble() * 480);
            from.Add(p);
            isOutlier[i] = random.NextDouble() < 0.3;
            to.Add(isOutlier[i]
                ? new Point2(random.NextDouble() * 640, random.NextDouble() * 480)
                : Projective.Map(p) with { X = Projective.Map(p).X + Noise(random, 0.3) });
        }

        var estimate = RansacHomography.Estimate(from, to, new RansacOptions { Seed = 7 })!;

        AssertClose(Projective, estimate.Homography, 2e-3);
        var correctlyClassified = Enumerable.Range(0, count).Count(i => estimate.Inliers[i] != isOutlier[i]);
        Assert.True(correctlyClassified >= 0.98 * count, $"{correctlyClassified} of {count} classified correctly.");
        Assert.True(estimate.Iterations < 2000, "The adaptive iteration count should stop early at 70% inliers.");
    }

    [Fact]
    public void Ransac_is_deterministic_for_a_seed()
    {
        var random = new Random(2);
        var from = Enumerable.Range(0, 60).Select(_ => new Point2(random.NextDouble() * 300, random.NextDouble() * 300)).ToList();
        var to = from.Select((p, i) => i % 3 == 0 ? new Point2(p.Y, p.X) : Projective.Map(p)).ToList();

        var a = RansacHomography.Estimate(from, to, new RansacOptions { Seed = 42 })!;
        var b = RansacHomography.Estimate(from, to, new RansacOptions { Seed = 42 })!;

        Assert.Equal(a.Homography.ToRowMajor(), b.Homography.ToRowMajor());
        Assert.Equal(a.Inliers, b.Inliers);
    }

    [Theory]
    [InlineData(0.5, 0.99, 72)]
    [InlineData(0.9, 0.995, 5)]
    [InlineData(1.0, 0.995, 1)]
    public void Adaptive_iteration_count_follows_the_standard_formula(double inlierRatio, double confidence, int expected)
    {
        Assert.Equal(expected, RansacHomography.AdaptiveIterations(inlierRatio, confidence));
    }

    private static double Noise(Random random, double sigma)
    {
        // Box–Muller.
        var u = 1 - random.NextDouble();
        var v = random.NextDouble();
        return sigma * Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v);
    }

    private static void AssertClose(Homography expected, Homography actual, double tolerance)
    {
        var e = expected.ToRowMajor();
        var a = actual.ToRowMajor();
        for (var i = 0; i < 9; i++)
        {
            var scale = Math.Max(1, Math.Abs(e[i]));
            Assert.True(Math.Abs(e[i] - a[i]) / scale < tolerance, $"H[{i}] = {a[i]}, expected {e[i]}.");
        }
    }
}
