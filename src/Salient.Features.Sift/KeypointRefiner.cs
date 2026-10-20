namespace Salient.Features.Sift;

/// <summary>
/// Refines extrema to sub-pixel, sub-scale accuracy by fitting a 3-D quadratic to D (Lowe 2004, section 4; Brown and Lowe 2002).
/// </summary>
internal static class KeypointRefiner
{
    /// <summary>Maximum number of moves to a neighbouring sample before the candidate is declared unstable.</summary>
    public const int MaxIterations = 5;

    /// <summary>Refines all candidates in parallel; results keep the input order.</summary>
    public static (List<RefinedPoint> Kept, List<ScaleSpacePoint> Rejected) RefineAll(
        ScaleSpace scaleSpace,
        IReadOnlyList<ScaleSpacePoint> candidates,
        CancellationToken cancellationToken)
    {
        var results = new RefinedPoint?[candidates.Count];
        Parallel.For(
            0,
            candidates.Count,
            new ParallelOptions { CancellationToken = cancellationToken },
            i => results[i] = Refine(scaleSpace, candidates[i]));

        var kept = new List<RefinedPoint>(candidates.Count);
        var rejected = new List<ScaleSpacePoint>();
        for (var i = 0; i < results.Length; i++)
        {
            if (results[i] is { } refined)
            {
                kept.Add(refined);
            }
            else
            {
                rejected.Add(candidates[i]);
            }
        }

        return (kept, rejected);
    }

    /// <summary>
    /// Iterates x̂ = −H⁻¹∇D, moving to the neighbouring sample whenever an offset exceeds 0.5,
    /// always from the current (accumulated) location. Returns <see langword="null"/> when the fit does not converge,
    /// leaves the searchable range, or the Hessian is singular.
    /// </summary>
    public static RefinedPoint? Refine(ScaleSpace scaleSpace, ScaleSpacePoint candidate)
    {
        var octave = scaleSpace.Octaves[candidate.Octave];
        var s = scaleSpace.LayersPerOctave;
        var border = ExtremaDetector.ImageBorder;
        var (layer, x, y) = (candidate.Layer, candidate.X, candidate.Y);

        double ox = 0, oy = 0, os = 0;
        var gradient = new double[3];
        var hessian = new double[3, 3];
        var converged = false;

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            Derivatives(octave, layer, x, y, gradient, hessian);
            if (!SolveNegated(hessian, gradient, out ox, out oy, out os))
            {
                return null;
            }

            if (Math.Abs(ox) < 0.5 && Math.Abs(oy) < 0.5 && Math.Abs(os) < 0.5)
            {
                converged = true;
                break;
            }

            const double limit = int.MaxValue / 3.0;
            if (Math.Abs(ox) > limit || Math.Abs(oy) > limit || Math.Abs(os) > limit)
            {
                return null;
            }

            x += (int)Math.Round(ox);
            y += (int)Math.Round(oy);
            layer += (int)Math.Round(os);

            if (layer < 1 || layer > s ||
                x < border || x >= octave.Width - border ||
                y < border || y >= octave.Height - border)
            {
                return null;
            }
        }

        if (!converged)
        {
            return null;
        }

        // Value of the quadratic at the extremum: D(x̂) = D + ½ ∇Dᵀ x̂, with ∇D taken at the final sample.
        var value = octave.Dogs[layer][x, y] + (0.5 * ((gradient[0] * ox) + (gradient[1] * oy) + (gradient[2] * os)));

        return new RefinedPoint(
            new ScaleSpacePoint(candidate.Octave, layer, x, y),
            (float)ox,
            (float)oy,
            (float)os,
            (float)value,
            (float)(Math.Abs(value) * s),
            float.NaN);
    }

    /// <summary>
    /// Central-difference gradient (∂x, ∂y, ∂σ) and Hessian of D at a sample.
    /// First derivatives are halved differences; second derivatives use the 3-point stencil.
    /// </summary>
    private static void Derivatives(Octave octave, int layer, int x, int y, double[] gradient, double[,] hessian)
    {
        var below = octave.Dogs[layer - 1];
        var current = octave.Dogs[layer];
        var above = octave.Dogs[layer + 1];
        double v = current[x, y];

        gradient[0] = 0.5 * (current[x + 1, y] - current[x - 1, y]);
        gradient[1] = 0.5 * (current[x, y + 1] - current[x, y - 1]);
        gradient[2] = 0.5 * (above[x, y] - below[x, y]);

        var dxx = current[x + 1, y] + current[x - 1, y] - (2 * v);
        var dyy = current[x, y + 1] + current[x, y - 1] - (2 * v);
        var dss = above[x, y] + below[x, y] - (2 * v);
        var dxy = 0.25 * (current[x + 1, y + 1] - current[x - 1, y + 1] - current[x + 1, y - 1] + current[x - 1, y - 1]);
        var dxs = 0.25 * (above[x + 1, y] - above[x - 1, y] - below[x + 1, y] + below[x - 1, y]);
        var dys = 0.25 * (above[x, y + 1] - above[x, y - 1] - below[x, y + 1] + below[x, y - 1]);

        hessian[0, 0] = dxx;
        hessian[0, 1] = dxy;
        hessian[0, 2] = dxs;
        hessian[1, 0] = dxy;
        hessian[1, 1] = dyy;
        hessian[1, 2] = dys;
        hessian[2, 0] = dxs;
        hessian[2, 1] = dys;
        hessian[2, 2] = dss;
    }

    /// <summary>Solves H·X = g by Gaussian elimination with partial pivoting and returns −X.</summary>
    internal static bool SolveNegated(double[,] h, double[] g, out double x0, out double x1, out double x2)
    {
        Span<double> a = stackalloc double[12]; // 3 rows × 4 columns, augmented with g
        for (var r = 0; r < 3; r++)
        {
            a[(r * 4) + 0] = h[r, 0];
            a[(r * 4) + 1] = h[r, 1];
            a[(r * 4) + 2] = h[r, 2];
            a[(r * 4) + 3] = g[r];
        }

        for (var col = 0; col < 3; col++)
        {
            var pivot = col;
            for (var r = col + 1; r < 3; r++)
            {
                if (Math.Abs(a[(r * 4) + col]) > Math.Abs(a[(pivot * 4) + col]))
                {
                    pivot = r;
                }
            }

            if (Math.Abs(a[(pivot * 4) + col]) < 1e-12)
            {
                x0 = x1 = x2 = 0;
                return false;
            }

            if (pivot != col)
            {
                for (var c = 0; c < 4; c++)
                {
                    (a[(col * 4) + c], a[(pivot * 4) + c]) = (a[(pivot * 4) + c], a[(col * 4) + c]);
                }
            }

            for (var r = col + 1; r < 3; r++)
            {
                var factor = a[(r * 4) + col] / a[(col * 4) + col];
                for (var c = col; c < 4; c++)
                {
                    a[(r * 4) + c] -= factor * a[(col * 4) + c];
                }
            }
        }

        var z = a[11] / a[10];
        var y = (a[7] - (a[6] * z)) / a[5];
        var x = (a[3] - (a[1] * y) - (a[2] * z)) / a[0];
        (x0, x1, x2) = (-x, -y, -z);
        return true;
    }
}
