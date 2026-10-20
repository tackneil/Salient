namespace Salient.Matching;

/// <summary>Eigen-decomposition of small symmetric matrices with the cyclic Jacobi method.</summary>
internal static class SymmetricEigen
{
    private const int MaxSweeps = 100;

    /// <summary>Returns the unit eigenvector of the smallest eigenvalue of a symmetric matrix. The input is not modified.</summary>
    public static double[] SmallestEigenvector(double[,] matrix)
    {
        var n = matrix.GetLength(0);
        var a = (double[,])matrix.Clone();
        var v = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            v[i, i] = 1;
        }

        for (var sweep = 0; sweep < MaxSweeps; sweep++)
        {
            double offDiagonal = 0;
            for (var p = 0; p < n; p++)
            {
                for (var q = p + 1; q < n; q++)
                {
                    offDiagonal += a[p, q] * a[p, q];
                }
            }

            if (offDiagonal < 1e-30)
            {
                break;
            }

            for (var p = 0; p < n; p++)
            {
                for (var q = p + 1; q < n; q++)
                {
                    if (Math.Abs(a[p, q]) < 1e-300)
                    {
                        continue;
                    }

                    // Rotation that zeroes a[p, q].
                    var theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    var t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt((theta * theta) + 1));
                    if (theta == 0)
                    {
                        t = 1;
                    }

                    var c = 1 / Math.Sqrt((t * t) + 1);
                    var s = t * c;

                    for (var k = 0; k < n; k++)
                    {
                        var akp = a[k, p];
                        var akq = a[k, q];
                        a[k, p] = (c * akp) - (s * akq);
                        a[k, q] = (s * akp) + (c * akq);
                    }

                    for (var k = 0; k < n; k++)
                    {
                        var apk = a[p, k];
                        var aqk = a[q, k];
                        a[p, k] = (c * apk) - (s * aqk);
                        a[q, k] = (s * apk) + (c * aqk);
                    }

                    for (var k = 0; k < n; k++)
                    {
                        var vkp = v[k, p];
                        var vkq = v[k, q];
                        v[k, p] = (c * vkp) - (s * vkq);
                        v[k, q] = (s * vkp) + (c * vkq);
                    }
                }
            }
        }

        var smallest = 0;
        for (var i = 1; i < n; i++)
        {
            if (a[i, i] < a[smallest, smallest])
            {
                smallest = i;
            }
        }

        var result = new double[n];
        for (var k = 0; k < n; k++)
        {
            result[k] = v[k, smallest];
        }

        return result;
    }
}
