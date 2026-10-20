using System.Numerics;
using System.Runtime.InteropServices;

namespace Salient.Core;

/// <summary>SIMD helpers for row operations.</summary>
internal static class VectorOps
{
    /// <summary>Computes <c>acc[i] += source[i] * weight</c>.</summary>
    public static void AddScaled(Span<float> acc, ReadOnlySpan<float> source, float weight)
    {
        var i = 0;
        if (Vector.IsHardwareAccelerated && acc.Length >= Vector<float>.Count)
        {
            var accVectors = MemoryMarshal.Cast<float, Vector<float>>(acc);
            var sourceVectors = MemoryMarshal.Cast<float, Vector<float>>(source[..acc.Length]);
            var w = new Vector<float>(weight);
            for (var v = 0; v < accVectors.Length; v++)
            {
                accVectors[v] += sourceVectors[v] * w;
            }

            i = accVectors.Length * Vector<float>.Count;
        }

        for (; i < acc.Length; i++)
        {
            acc[i] += source[i] * weight;
        }
    }

    /// <summary>Computes <c>destination[i] = left[i] - right[i]</c>.</summary>
    public static void Subtract(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        var i = 0;
        if (Vector.IsHardwareAccelerated && destination.Length >= Vector<float>.Count)
        {
            var l = MemoryMarshal.Cast<float, Vector<float>>(left[..destination.Length]);
            var r = MemoryMarshal.Cast<float, Vector<float>>(right[..destination.Length]);
            var d = MemoryMarshal.Cast<float, Vector<float>>(destination);
            for (var v = 0; v < d.Length; v++)
            {
                d[v] = l[v] - r[v];
            }

            i = d.Length * Vector<float>.Count;
        }

        for (; i < destination.Length; i++)
        {
            destination[i] = left[i] - right[i];
        }
    }
}
