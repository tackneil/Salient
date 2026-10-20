using Salient.Core;

namespace Salient.Core.Tests;

public sealed class ImageMathTests
{
    [Fact]
    public void Subtract_keeps_the_sign()
    {
        var a = TestImages.Constant(17, 3, 0.25f);
        var b = TestImages.Constant(17, 3, 0.75f);

        var difference = ImageMath.Subtract(a, b);

        Assert.All(difference.Pixels.ToArray(), p => Assert.Equal(-0.5f, p));
    }

    [Fact]
    public void Subtract_matches_scalar_arithmetic_for_odd_lengths()
    {
        var a = TestImages.Random(13, 7, seed: 1);
        var b = TestImages.Random(13, 7, seed: 2);

        var difference = ImageMath.Subtract(a, b);

        for (var i = 0; i < a.Pixels.Length; i++)
        {
            Assert.Equal(a.Pixels[i] - b.Pixels[i], difference.Pixels[i]);
        }
    }

    [Fact]
    public void Gradients_of_a_linear_ramp_are_its_slopes_inside_the_image()
    {
        var ramp = TestImages.Ramp(12, 10, slopeX: 0.03f, slopeY: -0.05f);

        var (dx, dy) = ImageMath.Gradients(ramp);

        for (var y = 1; y < 9; y++)
        {
            for (var x = 1; x < 11; x++)
            {
                Assert.Equal(0.03f, dx[x, y], 5);
                Assert.Equal(-0.05f, dy[x, y], 5);
            }
        }

        // At the border one neighbour is replaced by the edge pixel, so the difference is halved.
        Assert.Equal(0.015f, dx[0, 5], 5);
    }
}
