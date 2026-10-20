namespace Salient.Core;

/// <summary>Border handling for operations that read outside the image.</summary>
public static class Border
{
    /// <summary>
    /// Maps an index into [0, <paramref name="length"/>) by mirroring around the edge pixels without repeating them
    /// (…, 2, 1 | 0, 1, 2, …, n − 1 | n − 2, …), the same as OpenCV's <c>BORDER_REFLECT_101</c>.
    /// Works for any distance outside the range.
    /// </summary>
    public static int Reflect101(int index, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if ((uint)index < (uint)length)
        {
            return index;
        }

        if (length == 1)
        {
            return 0;
        }

        var period = (2 * length) - 2;
        index %= period;
        if (index < 0)
        {
            index += period;
        }

        return index < length ? index : period - index;
    }
}
