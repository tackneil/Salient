using Salient.Core;

namespace Salient.Features.Sift;

/// <summary>Builds the difference-of-Gaussians layers D = L(kσ) − L(σ) (Lowe 2004, section 3).</summary>
internal static class DogPyramid
{
    /// <summary>Fills <see cref="Octave.Dogs"/> with s + 2 signed differences of adjacent Gaussian layers.</summary>
    public static void Build(ScaleSpace scaleSpace, CancellationToken cancellationToken)
    {
        foreach (var octave in scaleSpace.Octaves)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var gaussians = octave.Gaussians;
            var dogs = new ImageF[gaussians.Length - 1];
            for (var i = 0; i < dogs.Length; i++)
            {
                // Signed: the sign tells bright blobs (negative) from dark blobs (positive).
                dogs[i] = ImageMath.Subtract(gaussians[i + 1], gaussians[i]);
            }

            octave.Dogs = dogs;
        }
    }
}
