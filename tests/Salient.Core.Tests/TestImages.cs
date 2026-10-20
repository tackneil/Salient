using Salient.Core;

namespace Salient.Core.Tests;

internal static class TestImages
{
    public static ImageF Random(int width, int height, int seed)
    {
        var random = new Random(seed);
        var image = new ImageF(width, height);
        var pixels = image.Pixels;
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = random.NextSingle();
        }

        return image;
    }

    public static ImageF Constant(int width, int height, float value)
    {
        var image = new ImageF(width, height);
        image.Pixels.Fill(value);
        return image;
    }

    public static ImageF Ramp(int width, int height, float slopeX, float slopeY)
    {
        var image = new ImageF(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                image[x, y] = (slopeX * x) + (slopeY * y);
            }
        }

        return image;
    }

    /// <summary>Straightforward 2-D correlation with the outer product of <paramref name="kernel"/>, in double precision.</summary>
    public static ImageF NaiveConvolve2D(ImageF source, float[] kernel)
    {
        var radius = kernel.Length / 2;
        var result = new ImageF(source.Width, source.Height);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                double sum = 0;
                for (var ky = -radius; ky <= radius; ky++)
                {
                    var sy = Border.Reflect101(y + ky, source.Height);
                    for (var kx = -radius; kx <= radius; kx++)
                    {
                        var sx = Border.Reflect101(x + kx, source.Width);
                        sum += (double)kernel[ky + radius] * kernel[kx + radius] * source[sx, sy];
                    }
                }

                result[x, y] = (float)sum;
            }
        }

        return result;
    }

    public static float MaxAbsDifference(ImageF a, ImageF b)
    {
        var max = 0f;
        var pa = a.Pixels;
        var pb = b.Pixels;
        for (var i = 0; i < pa.Length; i++)
        {
            max = MathF.Max(max, MathF.Abs(pa[i] - pb[i]));
        }

        return max;
    }
}
