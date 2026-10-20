using System.Runtime.InteropServices;
using SkiaSharp;

namespace Salient.Api.Images;

/// <summary>Thrown when an upload cannot be decoded.</summary>
public sealed class InvalidImageException(string message) : Exception(message);

/// <summary>An RGBA image, 4 bytes per pixel, row by row, unpremultiplied.</summary>
public sealed record RgbaImage(byte[] Pixels, int Width, int Height);

/// <summary>Image decoding, resizing and encoding at the API boundary, backed by SkiaSharp (ADR-0009).</summary>
public static class ImageCodec
{
    /// <summary>Decodes PNG, JPEG, WebP, GIF or BMP and applies the EXIF orientation.</summary>
    public static RgbaImage Decode(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var codec = SKCodec.Create(stream) ?? throw new InvalidImageException("The file is not a supported image (PNG, JPEG, WebP, GIF, BMP).");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var pixels = new byte[info.BytesSize];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var result = codec.GetPixels(info, handle.AddrOfPinnedObject());
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                throw new InvalidImageException($"The image could not be decoded ({result}).");
            }
        }
        finally
        {
            handle.Free();
        }

        return Orient(new RgbaImage(pixels, info.Width, info.Height), codec.EncodedOrigin);
    }

    /// <summary>Resizes with a high-quality cubic filter.</summary>
    public static RgbaImage Resize(RgbaImage image, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(image);
        using var source = ToBitmap(image);
        using var resized = source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul), new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new InvalidOperationException("Resizing failed.");
        return new RgbaImage(resized.Bytes, width, height);
    }

    /// <summary>Encodes as lossy WebP.</summary>
    public static byte[] EncodeWebp(RgbaImage image, int quality = 88) => Encode(image, SKEncodedImageFormat.Webp, quality);

    /// <summary>Encodes as PNG.</summary>
    public static byte[] EncodePng(RgbaImage image) => Encode(image, SKEncodedImageFormat.Png, 100);

    /// <summary>Expands 8-bit gray to opaque RGBA.</summary>
    public static RgbaImage FromGray(byte[] gray, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(gray);
        var rgba = new byte[gray.Length * 4];
        for (var i = 0; i < gray.Length; i++)
        {
            rgba[(i * 4) + 0] = rgba[(i * 4) + 1] = rgba[(i * 4) + 2] = gray[i];
            rgba[(i * 4) + 3] = 255;
        }

        return new RgbaImage(rgba, width, height);
    }

    /// <summary>ITU-R BT.601 luma (OpenCV's weights), with transparent pixels composited over white.</summary>
    public static byte[] ToGray(RgbaImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var p = image.Pixels;
        var gray = new byte[image.Width * image.Height];
        for (var i = 0; i < gray.Length; i++)
        {
            var a = p[(i * 4) + 3] / 255.0;
            var luma = (0.299 * p[i * 4]) + (0.587 * p[(i * 4) + 1]) + (0.114 * p[(i * 4) + 2]);
            gray[i] = (byte)Math.Clamp(Math.Round((luma * a) + (255 * (1 - a))), 0, 255);
        }

        return gray;
    }

    private static byte[] Encode(RgbaImage image, SKEncodedImageFormat format, int quality)
    {
        using var bitmap = ToBitmap(image);
        using var data = bitmap.Encode(format, quality) ?? throw new InvalidOperationException($"{format} encoding failed.");
        return data.ToArray();
    }

    private static SKBitmap ToBitmap(RgbaImage image)
    {
        var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        Marshal.Copy(image.Pixels, 0, bitmap.GetPixels(), image.Pixels.Length);
        return bitmap;
    }

    /// <summary>Applies an EXIF orientation by remapping pixels.</summary>
    private static RgbaImage Orient(RgbaImage image, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return image;
        }

        var (w, h) = (image.Width, image.Height);
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var (dw, dh) = swap ? (h, w) : (w, h);
        var source = MemoryMarshal.Cast<byte, uint>(image.Pixels);
        var result = new byte[image.Pixels.Length];
        var target = MemoryMarshal.Cast<byte, uint>(result.AsSpan());

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var (tx, ty) = origin switch
                {
                    SKEncodedOrigin.TopRight => (w - 1 - x, y),
                    SKEncodedOrigin.BottomRight => (w - 1 - x, h - 1 - y),
                    SKEncodedOrigin.BottomLeft => (x, h - 1 - y),
                    SKEncodedOrigin.LeftTop => (y, x),
                    SKEncodedOrigin.RightTop => (h - 1 - y, x),
                    SKEncodedOrigin.RightBottom => (h - 1 - y, w - 1 - x),
                    SKEncodedOrigin.LeftBottom => (y, w - 1 - x),
                    _ => (x, y),
                };
                target[(ty * dw) + tx] = source[(y * w) + x];
            }
        }

        return new RgbaImage(result, dw, dh);
    }
}
