namespace Salient.Core;

/// <summary>Changing image size.</summary>
public static class Resampling
{
    /// <summary>
    /// Halves the image by taking every second pixel, starting at (0, 0). The result is ⌊w/2⌋ × ⌊h/2⌋.
    /// The input must already be blurred enough to avoid aliasing; in SIFT it is the layer with σ = 2σ₀.
    /// </summary>
    public static ImageF Downsample2(ImageF source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Width < 2 || source.Height < 2)
        {
            throw new ArgumentException($"Cannot halve a {source.Width}×{source.Height} image.", nameof(source));
        }

        var result = new ImageF(source.Width / 2, source.Height / 2);
        for (var y = 0; y < result.Height; y++)
        {
            var sourceRow = source.Row(2 * y);
            var row = result.Row(y);
            for (var x = 0; x < row.Length; x++)
            {
                row[x] = sourceRow[2 * x];
            }
        }

        return result;
    }

    /// <summary>
    /// Resizes with bilinear interpolation, aligning pixel centers (the convention used by OpenCV's <c>INTER_LINEAR</c>).
    /// </summary>
    public static ImageF ResizeBilinear(ImageF source, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new ImageF(width, height);
        var scaleX = (float)source.Width / width;
        var scaleY = (float)source.Height / height;

        Parallel.For(0, height, y =>
        {
            var sourceY = ((y + 0.5f) * scaleY) - 0.5f;
            var row = result.Row(y);
            for (var x = 0; x < width; x++)
            {
                row[x] = source.SampleBilinear(((x + 0.5f) * scaleX) - 0.5f, sourceY);
            }
        });

        return result;
    }
}
