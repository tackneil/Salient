using Salient.Core;

namespace Salient.Features.Sift.Tests;

/// <summary>
/// A deterministic, procedurally generated test scene for golden tests: soft-edged shapes, textured patches and a
/// background gradient, quantized to 8 bits like a real photo. Avoids licensing questions around real images.
/// </summary>
internal static class GoldenScene
{
    public static ImageF Create(int width, int height, int seed)
    {
        var random = new Random(seed);
        var image = new ImageF(width, height);

        // Background: a gentle diagonal gradient.
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                image[x, y] = 0.35f + (0.2f * x / width) + (0.1f * y / height);
            }
        }

        // Textured patches: smoothed noise blended in with soft circular masks.
        var texture = SyntheticImages.Texture(width, height, seed + 1, blur: 1.5);
        for (var i = 0; i < 6; i++)
        {
            var (cx, cy, r) = (random.Next(width), random.Next(height), random.Next(30, 70));
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var d = Math.Sqrt(((x - cx) * (x - cx)) + ((y - cy) * (y - cy)));
                    var mask = (float)(1 / (1 + Math.Exp((d - r) / 2.0)));
                    image[x, y] += mask * 3f * (texture[x, y] - 0.5f);
                }
            }
        }

        // Shapes: ellipses and rectangles with soft (about one pixel) edges and random intensity changes.
        for (var i = 0; i < 60; i++)
        {
            var (cx, cy) = (random.Next(width), random.Next(height));
            var (a, b) = (random.Next(4, 40), random.Next(4, 40));
            var delta = (random.NextSingle() - 0.5f) * 0.6f;
            var ellipse = random.Next(2) == 0;
            for (var y = Math.Max(0, cy - b - 3); y < Math.Min(height, cy + b + 3); y++)
            {
                for (var x = Math.Max(0, cx - a - 3); x < Math.Min(width, cx + a + 3); x++)
                {
                    double inside;
                    if (ellipse)
                    {
                        var q = Math.Sqrt((((x - cx) / (double)a) * ((x - cx) / (double)a)) + (((y - cy) / (double)b) * ((y - cy) / (double)b)));
                        inside = (1 - q) * Math.Min(a, b);
                    }
                    else
                    {
                        inside = Math.Min(a - Math.Abs(x - cx), b - Math.Abs(y - cy));
                    }

                    image[x, y] += delta * (float)(1 / (1 + Math.Exp(-inside)));
                }
            }
        }

        // Camera-like blur, then 8-bit quantization so OpenCV and Salient see identical pixels.
        var blurred = GaussianBlur.Apply(image, 0.7);
        return ImageF.FromGray8(blurred.ToGray8(), width, height);
    }

    /// <summary>
    /// Rotates by <paramref name="degrees"/> counterclockwise on screen and scales by <paramref name="scale"/> around the
    /// image center, with bilinear sampling and a black border. Returns the warped image and the mapping from source
    /// to warped coordinates.
    /// </summary>
    public static (ImageF Image, Func<double, double, (double X, double Y)> Map) RotateAndScale(ImageF source, double degrees, double scale)
    {
        var (cx, cy) = ((source.Width - 1) / 2.0, (source.Height - 1) / 2.0);
        var t = degrees * Math.PI / 180;
        var (cos, sin) = (Math.Cos(t), Math.Sin(t));

        // Counterclockwise on screen with Y down: (dx, dy) → (dx·cos + dy·sin, −dx·sin + dy·cos).
        (double, double) Forward(double x, double y)
        {
            var (dx, dy) = (x - cx, y - cy);
            return (cx + (scale * ((dx * cos) + (dy * sin))), cy + (scale * ((-dx * sin) + (dy * cos))));
        }

        var result = new ImageF(source.Width, source.Height);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var (dx, dy) = ((x - cx) / scale, (y - cy) / scale);
                var sx = cx + ((dx * cos) - (dy * sin));
                var sy = cy + ((dx * sin) + (dy * cos));
                if (sx >= 0 && sx <= source.Width - 1 && sy >= 0 && sy <= source.Height - 1)
                {
                    result[x, y] = source.SampleBilinear((float)sx, (float)sy);
                }
            }
        }

        return (ImageF.FromGray8(result.ToGray8(), result.Width, result.Height), Forward);
    }
}
