using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Salient.Api.Storage;

/// <summary>File-system storage for images, layers and results (ADR-0008).</summary>
public sealed class BlobStore
{
    private readonly string _root;

    public BlobStore(IOptions<SalientOptions> options, IHostEnvironment environment)
    {
        var root = options.Value.StorageRoot;
        _root = Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(environment.ContentRootPath, root));
    }

    /// <summary>Absolute storage root; the SQLite database lives here too. Created on first use.</summary>
    public string Root => _root;

    /// <summary>Creates the storage root if it does not exist.</summary>
    public void EnsureRoot() => Directory.CreateDirectory(_root);

    public bool Exists(string path) => File.Exists(Resolve(path));

    public async Task WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var full = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, content, cancellationToken);
    }

    public void Write(string path, ReadOnlySpan<byte> content)
    {
        var full = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        using var stream = File.Create(full);
        stream.Write(content);
    }

    public Stream OpenRead(string path) => File.OpenRead(Resolve(path));

    public Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllBytesAsync(Resolve(path), cancellationToken);

    /// <summary>Writes a value as Brotli-compressed JSON.</summary>
    public void WriteJson<T>(string path, T value, JsonSerializerOptions options)
    {
        var full = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        using var file = File.Create(full);
        using var brotli = new BrotliStream(file, CompressionLevel.Fastest);
        JsonSerializer.Serialize(brotli, value, options);
    }

    /// <summary>Reads a value written by <see cref="WriteJson{T}"/>.</summary>
    public async Task<T?> ReadJsonAsync<T>(string path, JsonSerializerOptions options, CancellationToken cancellationToken = default)
    {
        await using var file = OpenRead(path);
        await using var brotli = new BrotliStream(file, CompressionMode.Decompress);
        return await JsonSerializer.DeserializeAsync<T>(brotli, options, cancellationToken);
    }

    public void DeleteFolder(string path)
    {
        var full = Resolve(path);
        if (Directory.Exists(full))
        {
            Directory.Delete(full, recursive: true);
        }
    }

    private string Resolve(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_root, path));
        if (!full.StartsWith(_root, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Path '{path}' escapes the storage root.", nameof(path));
        }

        return full;
    }
}
