using Salient.Core;

namespace Salient.Features.Sift.Tests;

internal static class SyntheticImages
{
    /// <summary>A Gaussian blob of standard deviation <paramref name="sigma"/> on a flat background.</summary>
    public static ImageF Blob(int size, double cx, double cy, double sigma, float amplitude = 0.6f, float background = 0.2f)
    {
        var image = new ImageF(size, size);
        AddBlob(image, cx, cy, sigma, amplitude);
        for (var i = 0; i < image.Pixels.Length; i++)
        {
            image.Pixels[i] += background;
        }

        return image;
    }

    public static void AddBlob(ImageF image, double cx, double cy, double sigma, float amplitude)
    {
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var d2 = ((x - cx) * (x - cx)) + ((y - cy) * (y - cy));
                image[x, y] += amplitude * (float)Math.Exp(-d2 / (2 * sigma * sigma));
            }
        }
    }

    /// <summary>A vertical step edge at <paramref name="edgeX"/>, softened with a Gaussian.</summary>
    public static ImageF Edge(int size, int edgeX, float left = 0.2f, float right = 0.8f, double blur = 1.5)
    {
        var image = new ImageF(size, size);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                image[x, y] = x < edgeX ? left : right;
            }
        }

        return GaussianBlur.Apply(image, blur);
    }

    /// <summary>A bright disk with a soft (logistic) boundary about one pixel wide.</summary>
    public static ImageF Disk(int size, double cx, double cy, double radius, float inside = 0.8f, float outside = 0.2f)
    {
        var image = new ImageF(size, size);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = Math.Sqrt(((x - cx) * (x - cx)) + ((y - cy) * (y - cy)));
                var t = 1 / (1 + Math.Exp(d - radius));
                image[x, y] = outside + ((inside - outside) * (float)t);
            }
        }

        return image;
    }

    /// <summary>Smoothed uniform noise: a textured image with many keypoints.</summary>
    public static ImageF Texture(int width, int height, int seed, double blur = 2.0)
    {
        var random = new Random(seed);
        var image = new ImageF(width, height);
        for (var i = 0; i < image.Pixels.Length; i++)
        {
            image.Pixels[i] = random.NextSingle();
        }

        return GaussianBlur.Apply(image, blur);
    }

    /// <summary>
    /// Rotates a square image by 90° counterclockwise as seen on screen. A pixel at offset (dx, dy) from the center
    /// moves to (dy, −dx); the image size must be odd so the center is a pixel.
    /// </summary>
    public static ImageF RotateCounterclockwise90(ImageF source)
    {
        var size = source.Width;
        var c = size / 2;
        var result = new ImageF(size, size);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var (dx, dy) = (x - c, y - c);
                result[c + dy, c - dx] = source[x, y];
            }
        }

        return result;
    }
}
