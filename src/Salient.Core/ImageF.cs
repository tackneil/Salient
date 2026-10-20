namespace Salient.Core;

/// <summary>
/// A single-channel image of 32-bit floats stored row by row without padding.
/// Intensities are expected in [0, 1]; intermediate results such as DoG layers or gradients may be negative.
/// </summary>
public sealed class ImageF
{
    private readonly float[] _pixels;

    /// <summary>Creates a zero-filled image.</summary>
    /// <param name="width">Width in pixels; must be positive.</param>
    /// <param name="height">Height in pixels; must be positive.</param>
    public ImageF(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        _pixels = new float[checked(width * height)];
    }

    private ImageF(int width, int height, float[] pixels)
    {
        Width = width;
        Height = height;
        _pixels = pixels;
    }

    /// <summary>Width in pixels.</summary>
    public int Width { get; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; }

    /// <summary>All pixels, row by row.</summary>
    public Span<float> Pixels => _pixels;

    /// <summary>Gets or sets the pixel at column <paramref name="x"/> and row <paramref name="y"/>.</summary>
    public float this[int x, int y]
    {
        get => _pixels[Index(x, y)];
        set => _pixels[Index(x, y)] = value;
    }

    /// <summary>Returns row <paramref name="y"/> as a span of <see cref="Width"/> pixels.</summary>
    public Span<float> Row(int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);
        return _pixels.AsSpan(y * Width, Width);
    }

    /// <summary>Returns <see langword="true"/> when both images have the same width and height.</summary>
    public bool HasSameSizeAs(ImageF other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Width == other.Width && Height == other.Height;
    }

    /// <summary>Creates a deep copy of this image.</summary>
    public ImageF Clone() => new(Width, Height, (float[])_pixels.Clone());

    /// <summary>
    /// Samples the image at a sub-pixel position with bilinear interpolation.
    /// Integer coordinates are pixel centers; positions outside the image are clamped to the border.
    /// </summary>
    public float SampleBilinear(float x, float y)
    {
        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);
        var x0 = (int)x;
        var y0 = (int)y;
        var x1 = Math.Min(x0 + 1, Width - 1);
        var y1 = Math.Min(y0 + 1, Height - 1);
        var fx = x - x0;
        var fy = y - y0;

        var top = _pixels[(y0 * Width) + x0] + (fx * (_pixels[(y0 * Width) + x1] - _pixels[(y0 * Width) + x0]));
        var bottom = _pixels[(y1 * Width) + x0] + (fx * (_pixels[(y1 * Width) + x1] - _pixels[(y1 * Width) + x0]));
        return top + (fy * (bottom - top));
    }

    /// <summary>Creates an image from 8-bit grayscale pixels, mapping 0..255 to 0..1.</summary>
    /// <param name="pixels">Pixels row by row; length must be <paramref name="width"/> × <paramref name="height"/>.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    public static ImageF FromGray8(ReadOnlySpan<byte> pixels, int width, int height)
    {
        var image = new ImageF(width, height);
        if (pixels.Length != image._pixels.Length)
        {
            throw new ArgumentException($"Expected {image._pixels.Length} pixels for a {width}×{height} image, got {pixels.Length}.", nameof(pixels));
        }

        const float scale = 1f / 255f;
        for (var i = 0; i < pixels.Length; i++)
        {
            image._pixels[i] = pixels[i] * scale;
        }

        return image;
    }

    /// <summary>Converts the image to 8-bit grayscale, clamping intensities to [0, 1] and rounding.</summary>
    public byte[] ToGray8()
    {
        var result = new byte[_pixels.Length];
        for (var i = 0; i < _pixels.Length; i++)
        {
            result[i] = (byte)MathF.Round(Math.Clamp(_pixels[i], 0f, 1f) * 255f);
        }

        return result;
    }

    private int Index(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Pixel ({x}, {y}) is outside a {Width}×{Height} image.");
        }

        return (y * Width) + x;
    }
}
