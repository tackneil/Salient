namespace Salient.Core;

/// <summary>A point in image coordinates (pixels, Y pointing down).</summary>
/// <param name="X">Horizontal coordinate.</param>
/// <param name="Y">Vertical coordinate.</param>
public readonly record struct Point2(double X, double Y)
{
    /// <summary>Euclidean distance to <paramref name="other"/>.</summary>
    public double DistanceTo(Point2 other) => Math.Sqrt(((X - other.X) * (X - other.X)) + ((Y - other.Y) * (Y - other.Y)));
}
