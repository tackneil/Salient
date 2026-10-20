namespace Salient.Core;

/// <summary>One-dimensional Gaussian kernels for separable blurring.</summary>
public static class GaussianKernel
{
    /// <summary>Returns the kernel radius used for <paramref name="sigma"/>: ⌈3σ⌉, at least 1.</summary>
    public static int RadiusFor(double sigma)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sigma);
        return Math.Max(1, (int)Math.Ceiling(3 * sigma));
    }

    /// <summary>
    /// Creates a normalized, symmetric Gaussian kernel of length 2r + 1 with r = <see cref="RadiusFor"/>.
    /// The weights sum to 1.
    /// </summary>
    public static float[] Create(double sigma)
    {
        var radius = RadiusFor(sigma);
        var kernel = new float[(2 * radius) + 1];
        var denominator = 2 * sigma * sigma;
        double sum = 0;
        for (var i = -radius; i <= radius; i++)
        {
            var weight = Math.Exp(-(i * i) / denominator);
            kernel[i + radius] = (float)weight;
            sum += weight;
        }

        for (var i = 0; i < kernel.Length; i++)
        {
            kernel[i] = (float)(kernel[i] / sum);
        }

        return kernel;
    }
}
