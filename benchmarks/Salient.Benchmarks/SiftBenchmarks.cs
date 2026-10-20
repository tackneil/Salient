using BenchmarkDotNet.Attributes;
using Salient.Core;
using Salient.Features;
using Salient.Features.Sift;

namespace Salient.Benchmarks;

/// <summary>Full SIFT on a 1 MP textured image (ADR-0009 target: under 500 ms without pipeline artifacts).</summary>
[MemoryDiagnoser]
public class SiftBenchmarks
{
    private readonly SiftDetector _detector = new();
    private ImageF _image = null!;

    [Params(false, true)]
    public bool Upsample { get; set; }

    [GlobalSetup]
    public void Setup() => _image = GaussianBlur.Apply(BenchmarkImages.Random(1024, 1024), 2.0);

    [Benchmark]
    public DetectionResult Detect() => _detector.Detect(_image, new SiftParameters { UpsampleInput = Upsample });

    [Benchmark]
    public DescriptorSet DetectAndCompute() =>
        _detector.DetectAndCompute(_image, new SiftParameters { UpsampleInput = Upsample }).Descriptors;
}
