using Salient.Core;

namespace Salient.Matching;

/// <summary>A 3×3 projective transformation between image planes, normalized so that H₂₂ = 1 when possible.</summary>
public sealed class Homography
{
    private readonly double[] _h;

    private Homography(double[] h) => _h = h;

    /// <summary>The identity transformation.</summary>
    public static Homography Identity { get; } = new([1, 0, 0, 0, 1, 0, 0, 0, 1]);

    /// <summary>Element at <paramref name="row"/>, <paramref name="column"/>.</summary>
    public double this[int row, int column] => _h[(row * 3) + column];

    /// <summary>
    /// Approximate rotation in degrees, counterclockwise on screen, assuming the transformation is close to a similarity:
    /// atan2(−H₁₀, H₀₀).
    /// </summary>
    public double RotationDegrees => Math.Atan2(-_h[3], _h[0]) * 180 / Math.PI;

    /// <summary>Approximate uniform scale, assuming the transformation is close to a similarity: √(H₀₀² + H₁₀²).</summary>
    public double Scale => Math.Sqrt((_h[0] * _h[0]) + (_h[3] * _h[3]));

    /// <summary>Creates a homography from 9 row-major values.</summary>
    public static Homography FromRowMajor(ReadOnlySpan<double> values)
    {
        if (values.Length != 9)
        {
            throw new ArgumentException("A homography has 9 values.", nameof(values));
        }

        var h = values.ToArray();
        if (Math.Abs(h[8]) > 1e-12)
        {
            var scale = 1 / h[8];
            for (var i = 0; i < 9; i++)
            {
                h[i] *= scale;
            }
        }

        return new Homography(h);
    }

    /// <summary>
    /// A similarity: rotation by <paramref name="degrees"/> counterclockwise on screen and uniform <paramref name="scale"/>
    /// around <paramref name="center"/>.
    /// </summary>
    public static Homography Similarity(double degrees, double scale, Point2 center)
    {
        var t = degrees * Math.PI / 180;
        var (a, b) = (scale * Math.Cos(t), scale * Math.Sin(t));

        // (dx, dy) → (dx·cos + dy·sin, −dx·sin + dy·cos) · scale, which turns content counterclockwise with Y down.
        return FromRowMajor(
        [
            a, b, center.X - (a * center.X) - (b * center.Y),
            -b, a, center.Y + (b * center.X) - (a * center.Y),
            0, 0, 1,
        ]);
    }

    /// <summary>Row-major copy of the 9 values.</summary>
    public double[] ToRowMajor() => (double[])_h.Clone();

    /// <summary>Maps a point; returns NaN coordinates for points mapped to infinity.</summary>
    public Point2 Map(Point2 p)
    {
        var w = (_h[6] * p.X) + (_h[7] * p.Y) + _h[8];
        if (Math.Abs(w) < 1e-12)
        {
            return new Point2(double.NaN, double.NaN);
        }

        return new Point2(
            ((_h[0] * p.X) + (_h[1] * p.Y) + _h[2]) / w,
            ((_h[3] * p.X) + (_h[4] * p.Y) + _h[5]) / w);
    }

    /// <summary>The inverse transformation, or <see langword="null"/> when the matrix is singular.</summary>
    public Homography? Inverse()
    {
        var h = _h;
        var c00 = (h[4] * h[8]) - (h[5] * h[7]);
        var c01 = (h[5] * h[6]) - (h[3] * h[8]);
        var c02 = (h[3] * h[7]) - (h[4] * h[6]);
        var det = (h[0] * c00) + (h[1] * c01) + (h[2] * c02);
        if (Math.Abs(det) < 1e-15)
        {
            return null;
        }

        var inv = 1 / det;
        return FromRowMajor(
        [
            c00 * inv, ((h[2] * h[7]) - (h[1] * h[8])) * inv, ((h[1] * h[5]) - (h[2] * h[4])) * inv,
            c01 * inv, ((h[0] * h[8]) - (h[2] * h[6])) * inv, ((h[2] * h[3]) - (h[0] * h[5])) * inv,
            c02 * inv, ((h[1] * h[6]) - (h[0] * h[7])) * inv, ((h[0] * h[4]) - (h[1] * h[3])) * inv,
        ]);
    }

    /// <summary>
    /// Estimates the homography mapping <paramref name="from"/> onto <paramref name="to"/> with the normalized direct linear
    /// transform (Hartley and Zisserman, algorithm 4.2): both point sets are moved to their centroid and scaled to a mean
    /// distance of √2, the 2n × 9 system is solved for the smallest eigenvector of AᵀA, and the normalization is undone.
    /// Needs at least 4 correspondences; returns <see langword="null"/> for degenerate input.
    /// </summary>
    public static Homography? Estimate(IReadOnlyList<Point2> from, IReadOnlyList<Point2> to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (from.Count != to.Count)
        {
            throw new ArgumentException("Point lists must have the same length.", nameof(to));
        }

        if (from.Count < 4)
        {
            return null;
        }

        if (!Normalization(from, out var ts) || !Normalization(to, out var td))
        {
            return null;
        }

        // Accumulate AᵀA directly; each correspondence contributes two rows of A.
        var ata = new double[9, 9];
        Span<double> row = stackalloc double[9];
        for (var i = 0; i < from.Count; i++)
        {
            var (x, y) = (ts.Scale * (from[i].X - ts.Cx), ts.Scale * (from[i].Y - ts.Cy));
            var (u, v) = (td.Scale * (to[i].X - td.Cx), td.Scale * (to[i].Y - td.Cy));

            row.Clear();
            (row[0], row[1], row[2]) = (-x, -y, -1);
            (row[6], row[7], row[8]) = (u * x, u * y, u);
            AddOuter(ata, row);

            row.Clear();
            (row[3], row[4], row[5]) = (-x, -y, -1);
            (row[6], row[7], row[8]) = (v * x, v * y, v);
            AddOuter(ata, row);
        }

        var hn = SymmetricEigen.SmallestEigenvector(ata);

        // H = Td⁻¹ · Hn · Ts, with T = [[s, 0, −s·cx], [0, s, −s·cy], [0, 0, 1]].
        var tsm = new double[] { ts.Scale, 0, -ts.Scale * ts.Cx, 0, ts.Scale, -ts.Scale * ts.Cy, 0, 0, 1 };
        var tdInv = new double[] { 1 / td.Scale, 0, td.Cx, 0, 1 / td.Scale, td.Cy, 0, 0, 1 };
        var h = Multiply(tdInv, Multiply(hn, tsm));
        if (h.Any(double.IsNaN) || Math.Abs(h[8]) < 1e-12)
        {
            return null;
        }

        return FromRowMajor(h);
    }

    private static bool Normalization(IReadOnlyList<Point2> points, out (double Cx, double Cy, double Scale) transform)
    {
        double cx = 0, cy = 0;
        foreach (var p in points)
        {
            cx += p.X;
            cy += p.Y;
        }

        cx /= points.Count;
        cy /= points.Count;
        double meanDistance = 0;
        foreach (var p in points)
        {
            meanDistance += Math.Sqrt(((p.X - cx) * (p.X - cx)) + ((p.Y - cy) * (p.Y - cy)));
        }

        meanDistance /= points.Count;
        transform = (cx, cy, meanDistance > 1e-12 ? Math.Sqrt(2) / meanDistance : 0);
        return meanDistance > 1e-12;
    }

    private static void AddOuter(double[,] m, ReadOnlySpan<double> r)
    {
        for (var i = 0; i < 9; i++)
        {
            if (r[i] == 0)
            {
                continue;
            }

            for (var j = 0; j < 9; j++)
            {
                m[i, j] += r[i] * r[j];
            }
        }
    }

    private static double[] Multiply(double[] a, double[] b)
    {
        var c = new double[9];
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                c[(i * 3) + j] = (a[i * 3] * b[j]) + (a[(i * 3) + 1] * b[3 + j]) + (a[(i * 3) + 2] * b[6 + j]);
            }
        }

        return c;
    }
}
