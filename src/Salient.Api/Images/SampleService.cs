using System.Globalization;
using Microsoft.Extensions.Options;

namespace Salient.Api.Images;

/// <summary>A bundled sample image.</summary>
/// <param name="Name">File name, used as the identifier.</param>
/// <param name="Title">Human-readable title derived from the file name.</param>
public sealed record SampleInfo(string Name, string Title);

/// <summary>The sample gallery: images shipped in the <c>samples</c> folder.</summary>
public sealed class SampleService(IOptions<SalientOptions> options, IHostEnvironment environment)
{
    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".webp"];

    private string Folder => Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.SamplesPath));

    public IReadOnlyList<SampleInfo> List()
    {
        if (!Directory.Exists(Folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(Folder)
            .Where(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Select(f => Path.GetFileName(f))
            .Order(StringComparer.Ordinal)
            .Select(name => new SampleInfo(name, Title(name)))
            .ToList();
    }

    /// <summary>The full path of a listed sample, or <see langword="null"/>; names outside the listing are rejected.</summary>
    public string? Find(string name) =>
        List().Any(s => s.Name == name) ? Path.Combine(Folder, name) : null;

    private static string Title(string fileName)
    {
        var words = Path.GetFileNameWithoutExtension(fileName).Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries);
        var text = string.Join(' ', words);
        return text.Length == 0 ? fileName : char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];
    }
}
