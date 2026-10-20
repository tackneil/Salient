using Salient.Core;

namespace Salient.Core.Tests;

public sealed class ConvolutionTests
{
    [Theory]
    [InlineData(37, 23, 0.8)]
    [InlineData(64, 48, 1.6)]
    [InlineData(19, 31, 3.2)]
    [InlineData(5, 4, 2.0)] // kernel wider than the image: reflection wraps several times
    [InlineData(1, 9, 1.0)]
    public void Separable_matches_naive_2D_reference(int width, int height, double sigma)
    {
        var source = TestImages.Random(width, height, seed: width * 31 + height);
        var kernel = GaussianKernel.Create(sigma);

        var fast = Convolution.Separable(source, kernel);
        var reference = TestImages.NaiveConvolve2D(source, kernel);

        Assert.True(TestImages.MaxAbsDifference(fast, reference) < 1e-5f);
    }

    [Fact]
    public void Blur_keeps_a_constant_image_constant()
    {
        var blurred = GaussianBlur.Apply(TestImages.Constant(40, 30, 0.42f), sigma: 2.5);

        Assert.All(blurred.Pixels.ToArray(), p => Assert.Equal(0.42f, p, 5));
    }

    [Fact]
    public void Impulse_response_is_the_outer_product_of_the_kernel()
    {
        const int size = 31;
        var impulse = new ImageF(size, size);
        impulse[15, 15] = 1f;
        var kernel = GaussianKernel.Create(1.6);
        var radius = kernel.Length / 2;

        var response = Convolution.Separable(impulse, kernel);

        for (var y = -radius; y <= radius; y++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                Assert.Equal(kernel[x + radius] * kernel[y + radius], response[15 + x, 15 + y], 6);
            }
        }

        Assert.Equal(1.0, response.Pixels.ToArray().Sum(p => (double)p), precision: 5);
    }

    [Fact]
    public void Destination_may_alias_the_source()
    {
        var source = TestImages.Random(33, 21, seed: 7);
        var kernel = GaussianKernel.Create(1.3);
        var expected = Convolution.Separable(source, kernel);

        Convolution.Separable(source, source, kernel);

        Assert.Equal(0f, TestImages.MaxAbsDifference(source, expected));
    }

    [Fact]
    public void Even_kernel_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Convolution.Separable(new ImageF(4, 4), [0.5f, 0.5f]));
    }
}
