namespace Salient.Core;

/// <summary>Pixel-wise arithmetic and finite differences.</summary>
public static class ImageMath
{
    /// <summary>Returns the signed difference <paramref name="left"/> − <paramref name="right"/>.</summary>
    public static ImageF Subtract(ImageF left, ImageF right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (!left.HasSameSizeAs(right))
        {
            throw new ArgumentException("Images must have the same size.", nameof(right));
        }

        var result = new ImageF(left.Width, left.Height);
        VectorOps.Subtract(left.Pixels, right.Pixels, result.Pixels);
        return result;
    }

    /// <summary>
    /// Computes horizontal and vertical derivatives with central differences, (I(x+1) − I(x−1)) / 2.
    /// At the border the missing neighbour is replaced by the edge pixel.
    /// </summary>
    public static (ImageF Dx, ImageF Dy) Gradients(ImageF image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var width = image.Width;
        var height = image.Height;
        var dx = new ImageF(width, height);
        var dy = new ImageF(width, height);

        Parallel.For(0, height, y =>
        {
            var row = image.Row(y);
            var above = image.Row(Math.Max(y - 1, 0));
            var below = image.Row(Math.Min(y + 1, height - 1));
            var dxRow = dx.Row(y);
            var dyRow = dy.Row(y);
            for (var x = 0; x < width; x++)
            {
                dxRow[x] = 0.5f * (row[Math.Min(x + 1, width - 1)] - row[Math.Max(x - 1, 0)]);
                dyRow[x] = 0.5f * (below[x] - above[x]);
            }
        });

        return (dx, dy);
    }
}
