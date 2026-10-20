using Salient.Core;

namespace Salient.Core.Tests;

public sealed class ResamplingTests
{
    [Fact]
    public void Downsample2_takes_every_second_pixel()
    {
        var source = TestImages.Random(9, 7, seed: 3);

        var half = Resampling.Downsample2(source);

        Assert.Equal(4, half.Width);
        Assert.Equal(3, half.Height);
        for (var y = 0; y < half.Height; y++)
        {
            for (var x = 0; x < half.Width; x++)
            {
                Assert.Equal(source[2 * x, 2 * y], half[x, y]);
            }
        }
    }

    [Fact]
    public void Resize_to_the_same_size_is_identity()
    {
        var source = TestImages.Random(20, 15, seed: 11);

        var resized = Resampling.ResizeBilinear(source, 20, 15);

        Assert.True(TestImages.MaxAbsDifference(source, resized) < 1e-6f);
    }

    [Fact]
    public void Resize_keeps_a_linear_ramp_linear_inside_the_image()
    {
        var source = TestImages.Ramp(40, 40, slopeX: 0.01f, slopeY: 0.02f);

        var resized = Resampling.ResizeBilinear(source, 20, 20);

        // Output pixel x maps to source x' = 2x + 0.5; away from the clamped border the ramp is reproduced exactly.
        for (var y = 1; y < 19; y++)
        {
            for (var x = 1; x < 19; x++)
            {
                Assert.Equal((0.01f * ((2 * x) + 0.5f)) + (0.02f * ((2 * y) + 0.5f)), resized[x, y], 5);
            }
        }
    }

    [Fact]
    public void SampleBilinear_interpolates_between_pixels_and_clamps_outside()
    {
        var image = new ImageF(2, 2);
        image[0, 0] = 0f;
        image[1, 0] = 1f;
        image[0, 1] = 2f;
        image[1, 1] = 3f;

        Assert.Equal(0f, image.SampleBilinear(0, 0));
        Assert.Equal(1.5f, image.SampleBilinear(0.5f, 0.5f), 6);
        Assert.Equal(0.25f, image.SampleBilinear(0.25f, 0), 6);
        Assert.Equal(3f, image.SampleBilinear(10, 10));
        Assert.Equal(0f, image.SampleBilinear(-4, -4));
    }
}
