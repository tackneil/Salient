using BenchmarkDotNet.Attributes;
using Salient.Features;
using Salient.Matching;

namespace Salient.Benchmarks;

/// <summary>Brute-force nearest-two search between two sets of 2000 descriptors.</summary>
[MemoryDiagnoser]
public class MatchingBenchmarks
{
    private const int Count = 2000;
    private DescriptorSet _realA = null!;
    private DescriptorSet _realB = null!;
    private DescriptorSet _binaryA = null!;
    private DescriptorSet _binaryB = null!;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        var keypoints = Enumerable.Repeat(new Keypoint(0, 0, 1, 0, 1, 0, 1), Count).ToList();
        float[] Floats() => Enumerable.Range(0, Count * 128).Select(_ => random.NextSingle()).ToArray();
        byte[] Bytes()
        {
            var bytes = new byte[Count * 32];
            random.NextBytes(bytes);
            return bytes;
        }

        _realA = DescriptorSet.FromFloats(keypoints, 128, Floats());
        _realB = DescriptorSet.FromFloats(keypoints, 128, Floats());
        _binaryA = DescriptorSet.FromBytes(keypoints, 32, Bytes());
        _binaryB = DescriptorSet.FromBytes(keypoints, 32, Bytes());
    }

    [Benchmark]
    public DescriptorMatch[] SiftL2() => BruteForceMatcher.NearestTwo(_realA, _realB);

    [Benchmark]
    public DescriptorMatch[] OrbHamming() => BruteForceMatcher.NearestTwo(_binaryA, _binaryB);
}
