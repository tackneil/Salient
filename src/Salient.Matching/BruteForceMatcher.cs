using System.Numerics;
using System.Runtime.InteropServices;
using Salient.Features;

namespace Salient.Matching;

/// <summary>Exhaustive nearest-neighbour search between two descriptor sets.</summary>
public static class BruteForceMatcher
{
    /// <summary>
    /// For every query descriptor, finds the nearest and second-nearest train descriptors. Real-valued descriptors use L2
    /// distance, binary ones Hamming distance (ADR-0003). Returns one match per query, in query order; an empty train set
    /// gives no matches.
    /// </summary>
    public static DescriptorMatch[] NearestTwo(DescriptorSet query, DescriptorSet train, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(train);
        if (query.Kind != train.Kind || query.Dimension != train.Dimension)
        {
            throw new ArgumentException($"Cannot match {query.Kind} descriptors of size {query.Dimension} with {train.Kind} descriptors of size {train.Dimension}.");
        }

        if (train.Count == 0 || query.Count == 0)
        {
            return [];
        }

        var matches = new DescriptorMatch[query.Count];
        var options = new ParallelOptions { CancellationToken = cancellationToken };
        if (query.Kind == DescriptorKind.Real)
        {
            Parallel.For(0, query.Count, options, i => matches[i] = Nearest(i, train, (t, j) => SquaredL2(query.Real(i), t.Real(j)), squared: true));
        }
        else
        {
            Parallel.For(0, query.Count, options, i => matches[i] = Nearest(i, train, (t, j) => Hamming(query.Binary(i), t.Binary(j)), squared: false));
        }

        return matches;
    }

    private static DescriptorMatch Nearest(int queryIndex, DescriptorSet train, Func<DescriptorSet, int, float> distance, bool squared)
    {
        var (best, second, bestIndex) = (float.PositiveInfinity, float.PositiveInfinity, -1);
        for (var j = 0; j < train.Count; j++)
        {
            var d = distance(train, j);
            if (d < best)
            {
                (second, best, bestIndex) = (best, d, j);
            }
            else if (d < second)
            {
                second = d;
            }
        }

        return squared
            ? new DescriptorMatch(queryIndex, bestIndex, MathF.Sqrt(best), MathF.Sqrt(second))
            : new DescriptorMatch(queryIndex, bestIndex, best, second);
    }

    /// <summary>Squared Euclidean distance.</summary>
    internal static float SquaredL2(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var sum = 0f;
        var i = 0;
        if (Vector.IsHardwareAccelerated && a.Length >= Vector<float>.Count)
        {
            var va = MemoryMarshal.Cast<float, Vector<float>>(a);
            var vb = MemoryMarshal.Cast<float, Vector<float>>(b[..a.Length]);
            var acc = Vector<float>.Zero;
            for (var v = 0; v < va.Length; v++)
            {
                var diff = va[v] - vb[v];
                acc += diff * diff;
            }

            sum = Vector.Sum(acc);
            i = va.Length * Vector<float>.Count;
        }

        for (; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            sum += diff * diff;
        }

        return sum;
    }

    /// <summary>Number of differing bits.</summary>
    internal static float Hamming(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var bits = 0;
        var i = 0;
        for (; i + 8 <= a.Length; i += 8)
        {
            bits += BitOperations.PopCount(MemoryMarshal.Read<ulong>(a[i..]) ^ MemoryMarshal.Read<ulong>(b[i..]));
        }

        for (; i < a.Length; i++)
        {
            bits += BitOperations.PopCount((uint)(a[i] ^ b[i]));
        }

        return bits;
    }
}
