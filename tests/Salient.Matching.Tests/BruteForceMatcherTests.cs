using Salient.Features;

namespace Salient.Matching.Tests;

public sealed class BruteForceMatcherTests
{
    private static readonly Keypoint Point = new(0, 0, 1, 0, 1, 0, 1);

    [Fact]
    public void Real_descriptors_use_L2_distance_and_report_the_second_nearest()
    {
        var query = Real([[0, 0, 0], [10, 10, 10]]);
        var train = Real([[3, 4, 0], [1, 0, 0], [10, 10, 12]]);

        var matches = BruteForceMatcher.NearestTwo(query, train);

        Assert.Equal(new DescriptorMatch(0, 1, 1, 5), matches[0]);
        Assert.Equal(1, matches[1].QueryIndex);
        Assert.Equal(2, matches[1].TrainIndex);
        Assert.Equal(2f, matches[1].Distance, 5);
        Assert.Equal(MathF.Sqrt(49 + 36 + 100), matches[1].SecondDistance, 4);
    }

    [Fact]
    public void Long_real_descriptors_match_scalar_arithmetic()
    {
        var random = new Random(5);
        var a = Enumerable.Range(0, 128).Select(_ => random.NextSingle()).ToArray();
        var b = Enumerable.Range(0, 128).Select(_ => random.NextSingle()).ToArray();

        var expected = a.Zip(b, (x, y) => (x - y) * (x - y)).Sum();

        Assert.Equal(expected, BruteForceMatcher.SquaredL2(a, b), 3);
    }

    [Fact]
    public void Binary_descriptors_use_Hamming_distance()
    {
        var a = new byte[11];
        var b = new byte[11];
        b[0] = 0b1011;   // 3 bits
        b[8] = 0xFF;     // 8 bits, in the scalar tail
        b[10] = 0b1;     // 1 bit

        Assert.Equal(12f, BruteForceMatcher.Hamming(a, b));

        var query = DescriptorSet.FromBytes([Point], 11, a);
        var train = DescriptorSet.FromBytes([Point, Point], 11, [.. b, .. new byte[11]]);
        var match = Assert.Single(BruteForceMatcher.NearestTwo(query, train));
        Assert.Equal(new DescriptorMatch(0, 1, 0, 12), match);
    }

    [Fact]
    public void Single_train_descriptor_has_an_infinite_second_distance()
    {
        var match = Assert.Single(BruteForceMatcher.NearestTwo(Real([[1, 1]]), Real([[1, 2]])));

        Assert.True(float.IsPositiveInfinity(match.SecondDistance));
        Assert.Equal(0f, match.Ratio);
    }

    [Fact]
    public void Mismatched_kinds_or_sizes_are_rejected()
    {
        var real = Real([[1, 2]]);
        var binary = DescriptorSet.FromBytes([Point], 2, [1, 2]);

        Assert.Throws<ArgumentException>(() => BruteForceMatcher.NearestTwo(real, binary));
        Assert.Throws<ArgumentException>(() => BruteForceMatcher.NearestTwo(real, Real([[1, 2, 3]])));
        Assert.Empty(BruteForceMatcher.NearestTwo(real, DescriptorSet.FromFloats([], 2, [])));
    }

    [Fact]
    public void Ratio_test_keeps_distinctive_matches_only()
    {
        DescriptorMatch[] matches = [new(0, 0, 1, 10), new(1, 1, 7, 10), new(2, 2, 8, 10), new(3, 3, 0, 0)];

        var kept = MatchFilters.RatioTest(matches, 0.8);

        Assert.Equal(new[] { 0, 1 }, kept.Select(m => m.QueryIndex));
        Assert.Equal(0.7f, matches[1].Ratio, 6);
        Assert.Equal(0f, matches[3].Ratio);
    }

    [Fact]
    public void Cross_check_keeps_mutual_nearest_neighbours()
    {
        DescriptorMatch[] forward = [new(0, 1, 1, 2), new(1, 1, 1, 2), new(2, 0, 1, 2)];
        DescriptorMatch[] backward = [new(0, 2, 1, 2), new(1, 0, 1, 2)];

        var kept = MatchFilters.CrossCheck(forward, backward);

        Assert.Equal(new[] { 0, 2 }, kept.Select(m => m.QueryIndex));
    }

    private static DescriptorSet Real(float[][] rows) =>
        DescriptorSet.FromFloats(rows.Select(_ => Point).ToList(), rows[0].Length, rows.SelectMany(r => r).ToArray());
}
