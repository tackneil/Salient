using Salient.Core;

namespace Salient.Core.Tests;

public sealed class ImageFTests
{
    [Fact]
    public void Gray8_round_trip_preserves_values()
    {
        byte[] pixels = [0, 1, 127, 128, 254, 255];

        var image = ImageF.FromGray8(pixels, 3, 2);

        Assert.Equal(1f, image[2, 1]);
        Assert.Equal(pixels, image.ToGray8());
    }

    [Fact]
    public void ToGray8_clamps_out_of_range_values()
    {
        var image = new ImageF(2, 1);
        image[0, 0] = -0.3f;
        image[1, 0] = 1.7f;

        Assert.Equal(new byte[] { 0, 255 }, image.ToGray8());
    }

    [Fact]
    public void FromGray8_rejects_a_wrong_pixel_count()
    {
        Assert.Throws<ArgumentException>(() => ImageF.FromGray8(new byte[5], 3, 2));
    }

    [Fact]
    public void Indexer_rejects_pixels_outside_the_image()
    {
        var image = new ImageF(3, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => image[3, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => image[0, -1]);
    }

    [Fact]
    public void Clone_is_independent()
    {
        var image = new ImageF(2, 2);
        var copy = image.Clone();

        copy[0, 0] = 1f;

        Assert.Equal(0f, image[0, 0]);
    }
}
