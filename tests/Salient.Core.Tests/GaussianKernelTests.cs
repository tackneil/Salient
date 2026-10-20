using Salient.Core;

namespace Salient.Core.Tests;

public sealed class GaussianKernelTests
{
    [Theory]
    [InlineData(0.5, 2)]
    [InlineData(1.0, 3)]
    [InlineData(1.6, 5)]
    [InlineData(3.2, 10)]
    public void Radius_is_ceiling_of_three_sigma(double sigma, int expectedRadius)
    {
        Assert.Equal(expectedRadius, GaussianKernel.RadiusFor(sigma));
        Assert.Equal((2 * expectedRadius) + 1, GaussianKernel.Create(sigma).Length);
    }

    [Theory]
    [InlineData(0.3)]
    [InlineData(1.6)]
    [InlineData(5.0)]
    public void Kernel_is_normalized_and_symmetric(double sigma)
    {
        var kernel = GaussianKernel.Create(sigma);

        Assert.Equal(1.0, kernel.Sum(w => (double)w), precision: 6);
        for (var i = 0; i < kernel.Length / 2; i++)
        {
            Assert.Equal(kernel[i], kernel[kernel.Length - 1 - i]);
        }

        Assert.Equal(kernel.Max(), kernel[kernel.Length / 2]);
    }

    [Fact]
    public void Incremental_sigma_composes_in_quadrature()
    {
        Assert.Equal(1.2, GaussianBlur.IncrementalSigma(1.6, 2.0), precision: 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => GaussianBlur.IncrementalSigma(2.0, 1.6));
    }
}
