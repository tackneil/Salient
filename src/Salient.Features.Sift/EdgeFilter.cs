namespace Salient.Features.Sift;

/// <summary>
/// Rejects extrema that lie on edges, where D has a large principal curvature across the edge but a small one along it
/// (Lowe 2004, section 4.1).
/// </summary>
internal static class EdgeFilter
{
    /// <summary>
    /// Keeps points whose ratio of principal curvatures is below the threshold: tr²(H) / det(H) &lt; (r + 1)² / r.
    /// Points with det(H) ≤ 0 (curvatures of opposite sign, a saddle) are rejected. The computed ratio is stored in
    /// <see cref="RefinedPoint.EdgeRatio"/> and the decision is made on that stored value.
    /// </summary>
    public static (List<RefinedPoint> Kept, List<RefinedPoint> Rejected) Apply(
        ScaleSpace scaleSpace,
        IReadOnlyList<RefinedPoint> points,
        double edgeThreshold)
    {
        var limit = MaxRatio(edgeThreshold);
        var kept = new List<RefinedPoint>(points.Count);
        var rejected = new List<RefinedPoint>();
        foreach (var point in points)
        {
            var withRatio = point with { EdgeRatio = Ratio(scaleSpace, point.Location) };
            (withRatio.EdgeRatio < limit ? kept : rejected).Add(withRatio);
        }

        return (kept, rejected);
    }

    /// <summary>The largest allowed tr²/det for edge threshold r: (r + 1)² / r.</summary>
    public static float MaxRatio(double edgeThreshold) =>
        (float)((edgeThreshold + 1) * (edgeThreshold + 1) / edgeThreshold);

    /// <summary>tr²(H) / det(H) of the spatial 2×2 Hessian of D at a sample, or positive infinity when det(H) ≤ 0.</summary>
    public static float Ratio(ScaleSpace scaleSpace, ScaleSpacePoint location)
    {
        var dog = scaleSpace.Octaves[location.Octave].Dogs[location.Layer];
        var (x, y) = (location.X, location.Y);
        double v = dog[x, y];
        var dxx = dog[x + 1, y] + dog[x - 1, y] - (2 * v);
        var dyy = dog[x, y + 1] + dog[x, y - 1] - (2 * v);
        var dxy = 0.25 * (dog[x + 1, y + 1] - dog[x - 1, y + 1] - dog[x + 1, y - 1] + dog[x - 1, y - 1]);
        return RatioFromHessian(dxx, dyy, dxy);
    }

    /// <summary>tr² / det for a 2×2 symmetric matrix [[dxx, dxy], [dxy, dyy]], or positive infinity when det ≤ 0.</summary>
    public static float RatioFromHessian(double dxx, double dyy, double dxy)
    {
        var trace = dxx + dyy;
        var determinant = (dxx * dyy) - (dxy * dxy);
        return determinant <= 0 ? float.PositiveInfinity : (float)(trace * trace / determinant);
    }
}
