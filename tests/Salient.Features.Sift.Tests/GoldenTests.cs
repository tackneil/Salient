using Salient.Reference.OpenCv;
using Xunit.Abstractions;

namespace Salient.Features.Sift.Tests;

/// <summary>
/// Compares Salient's SIFT with OpenCV's (ADR-0012). Both run with upsampling, which OpenCV always uses.
/// </summary>
[Trait("Category", "Golden")]
public sealed class GoldenTests(ITestOutputHelper output)
{
    private static readonly SiftParameters Parameters = new() { UpsampleInput = true };
    private static readonly OpenCvSiftParameters ReferenceParameters = new();
    private static readonly Lazy<Core.ImageF> Scene = new(() => GoldenScene.Create(480, 360, seed: 2026));

    private readonly SiftDetector _salient = new();
    private readonly OpenCvSiftDetector _opencv = new();

    [Fact]
    public void Keypoints_match_OpenCV()
    {
        Assert.True(OpenCvAvailability.IsAvailable, "The native OpenCV library is required for golden tests.");

        var ours = _salient.Detect(Scene.Value, Parameters).Keypoints;
        var reference = _opencv.Detect(Scene.Value, ReferenceParameters).Keypoints;

        var oursFound = Repeatability(ours, reference);
        var referenceFound = Repeatability(reference, ours);
        var angleDifference = MedianAngleDifference(ours, reference);
        output.WriteLine($"Salient {ours.Count}, OpenCV {reference.Count}; repeatability {oursFound:P1} / {referenceFound:P1}; median angle difference {angleDifference:F2}°");

        Assert.InRange(ours.Count / (double)reference.Count, 0.9, 1.1);
        Assert.True(oursFound >= 0.85, $"Only {oursFound:P1} of Salient keypoints have an OpenCV counterpart.");
        Assert.True(referenceFound >= 0.85, $"Only {referenceFound:P1} of OpenCV keypoints have a Salient counterpart.");
        Assert.True(angleDifference < 1, $"Median angle difference is {angleDifference:F2}°.");
    }

    [Fact]
    public void Descriptors_match_OpenCV_for_the_same_keypoints()
    {
        Assert.True(OpenCvAvailability.IsAvailable, "The native OpenCV library is required for golden tests.");

        var (detection, ours) = _salient.DetectAndCompute(Scene.Value, Parameters);
        var reference = _opencv.Compute(Scene.Value, detection.Keypoints, ReferenceParameters);

        // OpenCV may drop keypoints, so pair descriptors by keypoint position and orientation.
        var index = ours.Keypoints
            .Select((k, i) => (Key: (k.X, k.Y, MathF.Round(k.Angle, 2)), Index: i))
            .GroupBy(e => e.Key)
            .ToDictionary(g => g.Key, g => g.First().Index);
        var similarities = new List<double>();
        for (var i = 0; i < reference.Count; i++)
        {
            var k = reference.Keypoints[i];
            if (index.TryGetValue((k.X, k.Y, MathF.Round(k.Angle, 2)), out var j))
            {
                similarities.Add(Cosine(ours.Real(j), reference.Real(i)));
            }
        }

        similarities.Sort();
        var median = similarities[similarities.Count / 2];
        var tenth = similarities[similarities.Count / 10];
        output.WriteLine($"{similarities.Count} descriptor pairs; cosine similarity median {median:F4}, 10th percentile {tenth:F4}");

        Assert.True(similarities.Count > 0.9 * reference.Count, "Too few keypoints could be paired.");
        Assert.True(median >= 0.98, $"Median cosine similarity {median:F4}.");
        Assert.True(tenth >= 0.9, $"10th percentile cosine similarity {tenth:F4}.");
    }

    [Fact]
    public void Descriptors_match_a_rotated_and_scaled_copy()
    {
        var (warped, map) = GoldenScene.RotateAndScale(Scene.Value, degrees: 10, scale: 0.85);

        var (_, a) = _salient.DetectAndCompute(Scene.Value, Parameters);
        var (_, b) = _salient.DetectAndCompute(warped, Parameters);

        var matches = RatioTestMatches(a, b, ratio: 0.8);
        var correct = matches.Count(m =>
        {
            var (x, y) = map(a.Keypoints[m.A].X, a.Keypoints[m.A].Y);
            var (bx, by) = (b.Keypoints[m.B].X, b.Keypoints[m.B].Y);
            return Math.Sqrt(((x - bx) * (x - bx)) + ((y - by) * (y - by))) < 3;
        });
        var inlierRatio = correct / (double)matches.Count;
        output.WriteLine($"{matches.Count} matches after the ratio test, {correct} correct ({inlierRatio:P1})");

        Assert.True(matches.Count >= 100, $"Only {matches.Count} matches.");
        Assert.True(inlierRatio >= 0.9, $"Only {inlierRatio:P1} of matches are correct.");
    }

    [Fact]
    public void Binary_reference_detectors_produce_binary_descriptors()
    {
        Assert.True(OpenCvAvailability.IsAvailable, "The native OpenCV library is required for golden tests.");

        var (orbDetection, orb) = new OrbDetector().DetectAndCompute(Scene.Value, new OrbParameters());
        var (akazeDetection, akaze) = new AkazeDetector().DetectAndCompute(Scene.Value, new AkazeParameters());

        Assert.Equal(DescriptorKind.Binary, orb.Kind);
        Assert.Equal(32, orb.Dimension);
        Assert.Equal(orbDetection.Keypoints.Count, orb.Count);
        Assert.Equal(DescriptorKind.Binary, akaze.Kind);
        Assert.Equal(61, akaze.Dimension);
        Assert.Equal(akazeDetection.Keypoints.Count, akaze.Count);
        Assert.All(orbDetection.Keypoints, k => Assert.InRange(k.Angle, 0f, 360f));
    }

    private static double Repeatability(IReadOnlyList<Keypoint> from, IReadOnlyList<Keypoint> to)
    {
        var found = from.Count(p => to.Any(q =>
            Math.Abs(p.X - q.X) < 2 && Math.Abs(p.Y - q.Y) < 2 &&
            ((p.X - q.X) * (p.X - q.X)) + ((p.Y - q.Y) * (p.Y - q.Y)) < 4 &&
            Math.Abs((q.Scale / p.Scale) - 1) < 0.2));
        return found / (double)from.Count;
    }

    private static double MedianAngleDifference(IReadOnlyList<Keypoint> ours, IReadOnlyList<Keypoint> reference)
    {
        var differences = new List<double>();
        foreach (var p in ours)
        {
            var best = double.MaxValue;
            foreach (var q in reference)
            {
                if (Math.Abs(p.X - q.X) < 1 && Math.Abs(p.Y - q.Y) < 1 && Math.Abs((q.Scale / p.Scale) - 1) < 0.1)
                {
                    var d = Math.Abs(((q.Angle - p.Angle + 540) % 360) - 180);
                    best = Math.Min(best, d);
                }
            }

            if (best < double.MaxValue)
            {
                differences.Add(best);
            }
        }

        differences.Sort();
        return differences[differences.Count / 2];
    }

    private static double Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        return dot / Math.Sqrt(na * nb);
    }

    /// <summary>Brute-force nearest neighbours with Lowe's ratio test; a minimal stand-in until Salient.Matching exists.</summary>
    private static List<(int A, int B)> RatioTestMatches(DescriptorSet a, DescriptorSet b, double ratio)
    {
        var matches = new List<(int, int)>();
        for (var i = 0; i < a.Count; i++)
        {
            var (best, second, bestIndex) = (double.MaxValue, double.MaxValue, -1);
            var query = a.Real(i);
            for (var j = 0; j < b.Count; j++)
            {
                var candidate = b.Real(j);
                double d = 0;
                for (var k = 0; k < query.Length; k++)
                {
                    var diff = query[k] - candidate[k];
                    d += diff * diff;
                }

                if (d < best)
                {
                    (second, best, bestIndex) = (best, d, j);
                }
                else if (d < second)
                {
                    second = d;
                }
            }

            if (Math.Sqrt(best) < ratio * Math.Sqrt(second))
            {
                matches.Add((i, bestIndex));
            }
        }

        return matches;
    }
}
