using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Salient.Api.Storage;
using Salient.Core;

namespace Salient.Api.Images;

/// <summary>Image metadata returned by the API.</summary>
/// <param name="Id">Content-derived identifier.</param>
/// <param name="Width">Width after any downscaling; keypoint coordinates refer to this size.</param>
/// <param name="Height">Height after any downscaling.</param>
/// <param name="OriginalWidth">Width of the uploaded file.</param>
/// <param name="OriginalHeight">Height of the uploaded file.</param>
/// <param name="FileName">Original file name, if known.</param>
public sealed record ImageInfo(string Id, int Width, int Height, int OriginalWidth, int OriginalHeight, string? FileName)
{
    internal static ImageInfo From(ImageRecord r) => new(r.Id, r.Width, r.Height, r.OriginalWidth, r.OriginalHeight, r.FileName);
}

/// <summary>Decodes, normalizes and stores uploaded images ("load" stage, ADR-0004).</summary>
public sealed class ImageService(IDbContextFactory<SalientDbContext> dbFactory, BlobStore blobs, IOptions<SalientOptions> options)
{
    public static string GrayPath(string id) => $"images/{id}/gray.bin";

    public static string DisplayPath(string id) => $"images/{id}/display.webp";

    /// <summary>
    /// Decodes an upload, applies EXIF orientation, downscales it to the pixel limit, stores a grayscale copy for the
    /// algorithms and a color WebP for display, and returns its metadata. Identical pixels give the same id.
    /// </summary>
    public async Task<ImageInfo> StoreAsync(Stream content, string? fileName, CancellationToken cancellationToken)
    {
        var image = ImageCodec.Decode(content);
        var (originalWidth, originalHeight) = (image.Width, image.Height);
        var pixels = (long)image.Width * image.Height;
        var maxPixels = options.Value.MaxPixels;
        if (pixels > maxPixels)
        {
            var scale = Math.Sqrt(maxPixels / (double)pixels);
            image = ImageCodec.Resize(image, Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)));
        }

        var gray = ImageCodec.ToGray(image);
        var id = ContentId(gray, image.Width, image.Height);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Images.FindAsync([id], cancellationToken);
        if (existing is not null)
        {
            return ImageInfo.From(existing);
        }

        await blobs.WriteAsync(GrayPath(id), gray, cancellationToken);
        await blobs.WriteAsync(DisplayPath(id), ImageCodec.EncodeWebp(image, 85), cancellationToken);

        var record = new ImageRecord
        {
            Id = id,
            Width = image.Width,
            Height = image.Height,
            OriginalWidth = originalWidth,
            OriginalHeight = originalHeight,
            FileName = fileName is null ? null : Path.GetFileName(fileName),
            CreatedAt = DateTime.UtcNow,
        };
        db.Images.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return ImageInfo.From(record);
    }

    public async Task<ImageInfo?> FindAsync(string id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.Images.FindAsync([id], cancellationToken);
        return record is null ? null : ImageInfo.From(record);
    }

    /// <summary>Loads the stored grayscale pixels as an algorithm input.</summary>
    public async Task<ImageF> LoadGrayAsync(ImageInfo image, CancellationToken cancellationToken)
    {
        var bytes = await blobs.ReadAsync(GrayPath(image.Id), cancellationToken);
        return ImageF.FromGray8(bytes, image.Width, image.Height);
    }

    private static string ContentId(byte[] gray, int width, int height)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(BitConverter.GetBytes(width));
        sha.AppendData(BitConverter.GetBytes(height));
        sha.AppendData(gray);
        return Convert.ToHexStringLower(sha.GetHashAndReset())[..32];
    }
}
