using System.Buffers;

namespace Salient.Core;

/// <summary>Separable convolution with <see cref="Border.Reflect101"/> borders.</summary>
public static class Convolution
{
    /// <summary>
    /// Applies the same odd-length kernel along rows and then along columns.
    /// The kernel is applied as correlation, which equals convolution for the symmetric kernels used here.
    /// </summary>
    public static ImageF Separable(ImageF source, ReadOnlySpan<float> kernel)
    {
        ArgumentNullException.ThrowIfNull(source);
        var destination = new ImageF(source.Width, source.Height);
        Separable(source, destination, kernel);
        return destination;
    }

    /// <summary>
    /// Applies the same odd-length kernel along rows and then along columns, writing into <paramref name="destination"/>.
    /// <paramref name="destination"/> may be the same instance as <paramref name="source"/>.
    /// </summary>
    public static void Separable(ImageF source, ImageF destination, ReadOnlySpan<float> kernel)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        if (!source.HasSameSizeAs(destination))
        {
            throw new ArgumentException("Source and destination must have the same size.", nameof(destination));
        }

        if (kernel.Length % 2 == 0)
        {
            throw new ArgumentException("Kernel length must be odd.", nameof(kernel));
        }

        var weights = kernel.ToArray();
        var horizontal = new ImageF(source.Width, source.Height);
        HorizontalPass(source, horizontal, weights);
        VerticalPass(horizontal, destination, weights);
    }

    private static void HorizontalPass(ImageF source, ImageF destination, float[] kernel)
    {
        var width = source.Width;
        var radius = kernel.Length / 2;

        Parallel.For(0, source.Height, () => ArrayPool<float>.Shared.Rent(width + (2 * radius)), (y, _, padded) =>
        {
            // Copy the row into a buffer padded with reflected pixels so the inner loop needs no bounds logic.
            var row = source.Row(y);
            var buffer = padded.AsSpan(0, width + (2 * radius));
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = row[Border.Reflect101(i - radius, width)];
            }

            var output = destination.Row(y);
            output.Clear();
            for (var k = 0; k < kernel.Length; k++)
            {
                VectorOps.AddScaled(output, buffer.Slice(k, width), kernel[k]);
            }

            return padded;
        }, padded => ArrayPool<float>.Shared.Return(padded));
    }

    private static void VerticalPass(ImageF source, ImageF destination, float[] kernel)
    {
        var height = source.Height;
        var radius = kernel.Length / 2;

        Parallel.For(0, height, y =>
        {
            var output = destination.Row(y);
            output.Clear();
            for (var k = 0; k < kernel.Length; k++)
            {
                VectorOps.AddScaled(output, source.Row(Border.Reflect101(y + k - radius, height)), kernel[k]);
            }
        });
    }
}
