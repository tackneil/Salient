using Salient.Api.Images;
using Salient.Core;
using Salient.Features.Pipeline;

namespace Salient.Api.Rendering;

/// <summary>Value range and colormap of a rendered layer, for the UI legend.</summary>
/// <param name="Colormap"><c>gray</c>, <c>diverging</c>, <c>viridis</c> or <c>magma</c>.</param>
/// <param name="Min">Value at the low end of the colormap.</param>
/// <param name="Max">Value at the high end of the colormap.</param>
/// <param name="Scale"><c>linear</c> or <c>log</c>.</param>
public sealed record LayerLegend(string Colormap, double Min, double Max, string Scale = "linear");

/// <summary>A rendered image (WebP) and its legend.</summary>
public sealed record RenderedLayer(byte[] WebP, LayerLegend Legend);

/// <summary>Renders float layers to 8-bit WebP with the colormaps of the design mockup (ADR-0006).</summary>
public static class LayerRenderer
{
    private readonly record struct Stop(double T, byte R, byte G, byte B);

    // Viridis and magma follow matplotlib; the diverging map is the mockup's blue–ink–orange.
    private static readonly Stop[] Viridis = [new(0, 68, 1, 84), new(0.25, 59, 82, 139), new(0.5, 33, 145, 140), new(0.75, 94, 201, 98), new(1, 253, 231, 37)];
    private static readonly Stop[] Magma = [new(0, 0, 0, 4), new(0.25, 81, 18, 124), new(0.5, 183, 55, 121), new(0.75, 252, 137, 97), new(1, 252, 253, 191)];
    private static readonly Stop[] BlueInkOrange = [new(0, 90, 160, 255), new(0.5, 14, 18, 24), new(1, 255, 160, 60)];

    /// <summary>A Gaussian layer: intensities 0..1 in gray.</summary>
    public static RenderedLayer Gray(ImageF image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var webp = ImageCodec.EncodeWebp(ImageCodec.FromGray(image.ToGray8(), image.Width, image.Height));
        return new RenderedLayer(webp, new LayerLegend("gray", 0, 1));
    }

    /// <summary>A signed layer such as DoG: symmetric around 0, clipped at the 99.5th percentile of |value|.</summary>
    public static RenderedLayer Diverging(ImageF image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var limit = Math.Max(Percentile(image, 0.995), 1e-9);
        var webp = Colorize(image, (v, _) => 0.5 + (0.5 * Math.Clamp(v / limit, -1, 1)), BlueInkOrange);
        return new RenderedLayer(webp, new LayerLegend("diverging", -limit, limit));
    }

    /// <summary>A heatmap artifact with the colormap suited to its kind.</summary>
    /// <param name="heatmap">The map.</param>
    /// <param name="weight">For the edge-ratio map, the DoG response used to darken flat areas.</param>
    public static RenderedLayer Heatmap(HeatmapArtifact heatmap, ImageF? weight = null)
    {
        ArgumentNullException.ThrowIfNull(heatmap);
        var map = heatmap.Map;
        switch (heatmap.Kind)
        {
            case HeatmapKind.DogResponse:
                return Diverging(map);

            case HeatmapKind.EdgeRatio:
            {
                // tr²/det is at least 4; show log(4..1000), weighted by response strength so flat areas stay dark.
                var (low, high) = (Math.Log(4), Math.Log(1000));
                var weightLimit = weight is null ? 1 : Math.Max(Percentile(weight, 0.99), 1e-9);
                var webp = Colorize(map, (v, i) =>
                {
                    var t = float.IsPositiveInfinity(v) ? 1 : Math.Clamp((Math.Log(Math.Max(v, 4)) - low) / (high - low), 0, 1);
                    var w = weight is null ? 1 : Math.Clamp(Math.Abs(weight.Pixels[i]) / weightLimit, 0, 1);
                    return t * w;
                }, Magma);
                return new RenderedLayer(webp, new LayerLegend("magma", 4, 1000, "log"));
            }

            default:
            {
                var max = Math.Max(Percentile(map, 0.995), 1e-9);
                var webp = Colorize(map, (v, _) => Math.Clamp(v / max, 0, 1), Viridis);
                return new RenderedLayer(webp, new LayerLegend("viridis", 0, max));
            }
        }
    }

    private static byte[] Colorize(ImageF image, Func<float, int, double> toUnit, Stop[] stops)
    {
        var lut = new (byte R, byte G, byte B)[256];
        for (var i = 0; i < lut.Length; i++)
        {
            lut[i] = Interpolate(stops, i / 255.0);
        }

        var source = image.Pixels;
        var rgba = new byte[source.Length * 4];
        for (var i = 0; i < source.Length; i++)
        {
            var t = toUnit(source[i], i);
            var c = lut[(int)Math.Round(Math.Clamp(double.IsNaN(t) ? 0 : t, 0, 1) * 255)];
            (rgba[i * 4], rgba[(i * 4) + 1], rgba[(i * 4) + 2], rgba[(i * 4) + 3]) = (c.R, c.G, c.B, 255);
        }

        return ImageCodec.EncodeWebp(new RgbaImage(rgba, image.Width, image.Height));
    }

    private static (byte R, byte G, byte B) Interpolate(Stop[] stops, double t)
    {
        for (var i = 1; i < stops.Length; i++)
        {
            if (t <= stops[i].T)
            {
                var (a, b) = (stops[i - 1], stops[i]);
                var f = (t - a.T) / (b.T - a.T);
                return (Mix(a.R, b.R, f), Mix(a.G, b.G, f), Mix(a.B, b.B, f));
            }
        }

        return (stops[^1].R, stops[^1].G, stops[^1].B);
    }

    private static byte Mix(byte a, byte b, double f) => (byte)Math.Round(a + (f * (b - a)));

    /// <summary>Approximate percentile of |value| from at most 65 536 evenly spaced samples. Infinities are skipped.</summary>
    private static double Percentile(ImageF image, double quantile)
    {
        var pixels = image.Pixels;
        var step = Math.Max(1, pixels.Length / 65536);
        var samples = new List<double>((pixels.Length / step) + 1);
        for (var i = 0; i < pixels.Length; i += step)
        {
            if (float.IsFinite(pixels[i]))
            {
                samples.Add(Math.Abs(pixels[i]));
            }
        }

        if (samples.Count == 0)
        {
            return 0;
        }

        samples.Sort();
        return samples[Math.Min(samples.Count - 1, (int)(quantile * samples.Count))];
    }
}
