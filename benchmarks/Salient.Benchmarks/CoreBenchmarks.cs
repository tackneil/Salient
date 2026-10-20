using BenchmarkDotNet.Attributes;
using Salient.Core;

namespace Salient.Benchmarks;

/// <summary>Gaussian blur on a 1 MP image at the σ values typical for the first SIFT octave.</summary>
[MemoryDiagnoser]
public class GaussianBlurBenchmarks
{
    private ImageF _image = null!;

    [Params(1.6, 3.2, 6.4)]
    public double Sigma { get; set; }

    [GlobalSetup]
    public void Setup() => _image = BenchmarkImages.Random(1024, 1024);

    [Benchmark]
    public ImageF Blur() => GaussianBlur.Apply(_image, Sigma);
}

/// <summary>Resampling and pixel-wise operations on a 1 MP image.</summary>
[MemoryDiagnoser]
public class ResamplingBenchmarks
{
    private ImageF _image = null!;
    private ImageF _other = null!;

    [GlobalSetup]
    public void Setup()
    {
        _image = BenchmarkImages.Random(1024, 1024);
        _other = BenchmarkImages.Random(1024, 1024);
    }

    [Benchmark]
    public ImageF Downsample2() => Resampling.Downsample2(_image);

    [Benchmark]
    public ImageF ResizeBilinearToHalf() => Resampling.ResizeBilinear(_image, 512, 512);

    [Benchmark]
    public ImageF Subtract() => ImageMath.Subtract(_image, _other);

    [Benchmark]
    public (ImageF, ImageF) Gradients() => ImageMath.Gradients(_image);
}

internal static class BenchmarkImages
{
    public static ImageF Random(int width, int height)
    {
        var random = new Random(42);
        var image = new ImageF(width, height);
        var pixels = image.Pixels;
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = random.NextSingle();
        }

        return image;
    }
}
