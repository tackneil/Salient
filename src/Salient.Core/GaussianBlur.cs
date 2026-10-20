namespace Salient.Core;

/// <summary>Gaussian blurring.</summary>
public static class GaussianBlur
{
    /// <summary>Blurs <paramref name="source"/> with a Gaussian of standard deviation <paramref name="sigma"/> (in pixels).</summary>
    public static ImageF Apply(ImageF source, double sigma) =>
        Convolution.Separable(source, GaussianKernel.Create(sigma));

    /// <summary>
    /// Returns the σ that must be added to an image already blurred with <paramref name="currentSigma"/>
    /// to reach <paramref name="targetSigma"/>: √(target² − current²). Gaussian blurs compose in quadrature.
    /// </summary>
    public static double IncrementalSigma(double currentSigma, double targetSigma)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(currentSigma);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetSigma, currentSigma);
        return Math.Sqrt((targetSigma * targetSigma) - (currentSigma * currentSigma));
    }
}
