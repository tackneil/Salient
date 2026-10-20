using Salient.Core;
using Salient.Matching;

namespace Salient.Evaluation.Tests;

public sealed class TransformationsTests
{
    [Theory]
    [InlineData(TransformationFamily.Rotation, 30)]
    [InlineData(TransformationFamily.Rotation, 90)]
    [InlineData(TransformationFamily.Scale, 0.6)]
    [InlineData(TransformationFamily.Scale, 1.5)]
    public void Ground_truth_maps_reference_content_to_its_transformed_position(TransformationFamily family, double value)
    {
        var reference = new ImageF(121, 101);
        SyntheticBlob(reference, 80, 40);

        var result = Transformations.Apply(reference, family, value);

        var expected = result.GroundTruth.Map(new Point2(80, 40));
        var peak = Brightest(result.Image);
        Assert.True(peak.DistanceTo(expected) <= 1.0, $"Peak at {peak}, expected {expected}.");
    }

    [Fact]
    public void Photometric_families_keep_geometry_and_change_intensities()
    {
        var reference = GoldenSceneImage();

        var brighter = Transformations.Apply(reference, TransformationFamily.Brightness, 20);
        var noisy = Transformations.Apply(reference, TransformationFamily.Noise, 10, seed: 3);
        var noisyAgain = Transformations.Apply(reference, TransformationFamily.Noise, 10, seed: 3);

        Assert.Same(Homography.Identity, brighter.GroundTruth);
        Assert.True(brighter.Image.Pixels.ToArray().Average() > reference.Pixels.ToArray().Average() + 0.05);
        Assert.Equal(noisy.Image.Pixels.ToArray(), noisyAgain.Image.Pixels.ToArray());
        Assert.NotEqual(reference.Pixels.ToArray(), noisy.Image.Pixels.ToArray());
    }

    [Fact]
    public void Identity_values_leave_the_image_unchanged()
    {
        var reference = GoldenSceneImage();

        foreach (var family in Enum.GetValues<TransformationFamily>())
        {
            var result = Transformations.Apply(reference, family, Transformations.IdentityValue(family));
            Assert.Equal(reference.ToGray8(), result.Image.ToGray8());
        }
    }

    [Fact]
    public void Warp_fills_uncovered_pixels_with_zero()
    {
        var image = new ImageF(50, 50);
        image.Pixels.Fill(1f);

        var shifted = ImageWarp.Warp(image, Homography.FromRowMajor([1, 0, 20, 0, 1, 0, 0, 0, 1]), 50, 50);

        Assert.Equal(0f, shifted[5, 25]);
        Assert.Equal(1f, shifted[30, 25]);
    }

    private static ImageF GoldenSceneImage() => Features.Sift.Tests.GoldenScene.Create(160, 120, seed: 4);

    private static void SyntheticBlob(ImageF image, double cx, double cy)
    {
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var d2 = ((x - cx) * (x - cx)) + ((y - cy) * (y - cy));
                image[x, y] = (float)Math.Exp(-d2 / 8.0);
            }
        }
    }

    private static Point2 Brightest(ImageF image)
    {
        // Intensity-weighted centroid around the maximum gives a sub-pixel peak position.
        var (bx, by, best) = (0, 0, float.MinValue);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image[x, y] > best)
                {
                    (bx, by, best) = (x, y, image[x, y]);
                }
            }
        }

        double sx = 0, sy = 0, sw = 0;
        for (var y = Math.Max(0, by - 4); y <= Math.Min(image.Height - 1, by + 4); y++)
        {
            for (var x = Math.Max(0, bx - 4); x <= Math.Min(image.Width - 1, bx + 4); x++)
            {
                sx += x * image[x, y];
                sy += y * image[x, y];
                sw += image[x, y];
            }
        }

        return new Point2(sx / sw, sy / sw);
    }
}
