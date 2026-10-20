using Salient.Core;

namespace Salient.Features.Sift.Tests;

public sealed class ScaleSpaceTests
{
    [Fact]
    public void Layers_carry_the_intended_blur()
    {
        // An impulse blurred to σ has variance σ². The pyramid assumes the input already carries 0.5 of blur,
        // so the variance actually added to an impulse at nominal σ is σ² − 0.5².
        const int size = 121;
        var impulse = new ImageF(size, size);
        impulse[size / 2, size / 2] = 1f;
        var parameters = new SiftParameters { Octaves = 1 };

        var scaleSpace = GaussianPyramid.Build(impulse, parameters, CancellationToken.None);

        var layers = scaleSpace.Octaves[0].Gaussians;
        Assert.Equal(parameters.LayersPerOctave + 3, layers.Length);
        for (var i = 0; i < layers.Length; i++)
        {
            var nominal = parameters.Sigma * Math.Pow(2, (double)i / parameters.LayersPerOctave);
            var expectedVariance = (nominal * nominal) - 0.25;
            Assert.InRange(HorizontalVariance(layers[i]) / expectedVariance, 0.97, 1.03);
        }
    }

    [Fact]
    public void Next_octave_starts_from_layer_s_downsampled()
    {
        var image = SyntheticImages.Texture(96, 80, seed: 1);
        var parameters = new SiftParameters { Octaves = 2 };

        var scaleSpace = GaussianPyramid.Build(image, parameters, CancellationToken.None);

        var expected = Resampling.Downsample2(scaleSpace.Octaves[0].Gaussians[parameters.LayersPerOctave]);
        var actual = scaleSpace.Octaves[1].Gaussians[0];
        Assert.Equal(48, actual.Width);
        Assert.Equal(40, actual.Height);
        Assert.Equal(expected.Pixels.ToArray(), actual.Pixels.ToArray());
    }

    [Theory]
    [InlineData(0.6f, true)]
    [InlineData(-0.15f, false)]
    public void DoG_is_negative_at_a_bright_blob_and_positive_at_a_dark_one(float amplitude, bool bright)
    {
        var image = SyntheticImages.Blob(64, 32, 32, sigma: 3, amplitude: amplitude, background: 0.3f);
        var scaleSpace = GaussianPyramid.Build(image, new SiftParameters { Octaves = 1 }, CancellationToken.None);

        DogPyramid.Build(scaleSpace, CancellationToken.None);

        var center = scaleSpace.Octaves[0].Dogs[1][32, 32];
        Assert.True(bright ? center < 0 : center > 0, $"DoG at the center was {center}.");
    }

    [Fact]
    public void Automatic_octave_count_follows_OpenCV()
    {
        var image = new ImageF(900, 600);

        Assert.Equal(7, GaussianPyramid.OctaveCount(image, new SiftParameters()));
        Assert.Equal(9, GaussianPyramid.OctaveCount(image, new SiftParameters { UpsampleInput = true }));
        Assert.Equal(3, GaussianPyramid.OctaveCount(image, new SiftParameters { Octaves = 3 }));
    }

    [Fact]
    public void Hessian_solver_returns_the_negated_solution()
    {
        double[,] h = { { 4, 1, 0 }, { 1, 3, 1 }, { 0, 1, 2 } };
        double[] g = [1, 2, 3];

        Assert.True(KeypointRefiner.SolveNegated(h, g, out var x0, out var x1, out var x2));

        // Check H · (−x) = g.
        Assert.Equal(1, -((4 * x0) + x1), 9);
        Assert.Equal(2, -(x0 + (3 * x1) + x2), 9);
        Assert.Equal(3, -(x1 + (2 * x2)), 9);
        Assert.False(KeypointRefiner.SolveNegated(new double[3, 3], g, out _, out _, out _));
    }

    private static double HorizontalVariance(ImageF image)
    {
        double sum = 0, mean = 0, variance = 0;
        var c = image.Width / 2;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                sum += image[x, y];
                mean += image[x, y] * (x - c);
            }
        }

        mean /= sum;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                variance += image[x, y] * (x - c - mean) * (x - c - mean);
            }
        }

        return variance / sum;
    }
}
