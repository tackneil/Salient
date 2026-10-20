namespace Salient.Features.Sift;

/// <summary>Rejects low-contrast extrema, which are unstable under noise (Lowe 2004, section 4).</summary>
internal static class ContrastFilter
{
    /// <summary>
    /// Keeps points with |D(x̂)| · s ≥ <paramref name="threshold"/>.
    /// The comparison uses the stored <see cref="RefinedPoint.Contrast"/>, so diagnostics reproduce the decision exactly.
    /// </summary>
    public static (List<RefinedPoint> Kept, List<RefinedPoint> Rejected) Apply(IReadOnlyList<RefinedPoint> points, double threshold)
    {
        var limit = (float)threshold;
        var kept = new List<RefinedPoint>(points.Count);
        var rejected = new List<RefinedPoint>();
        foreach (var point in points)
        {
            (point.Contrast >= limit ? kept : rejected).Add(point);
        }

        return (kept, rejected);
    }
}
