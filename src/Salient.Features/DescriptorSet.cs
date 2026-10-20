using Salient.Core;

namespace Salient.Features;

/// <summary>How descriptor values are stored and compared.</summary>
public enum DescriptorKind
{
    /// <summary>Real-valued vectors compared with L2 distance, such as SIFT.</summary>
    Real,

    /// <summary>Bit strings compared with Hamming distance, such as ORB or AKAZE.</summary>
    Binary,
}

/// <summary>Descriptors for a list of keypoints, stored row by row in one flat array.</summary>
public sealed class DescriptorSet
{
    private readonly float[]? _floats;
    private readonly byte[]? _bytes;

    private DescriptorSet(IReadOnlyList<Keypoint> keypoints, int dimension, float[]? floats, byte[]? bytes)
    {
        Keypoints = keypoints;
        Dimension = dimension;
        _floats = floats;
        _bytes = bytes;
    }

    /// <summary>Float or binary.</summary>
    public DescriptorKind Kind => _floats is not null ? DescriptorKind.Real : DescriptorKind.Binary;

    /// <summary>The keypoints the descriptors belong to, aligned by index. An extractor may drop keypoints it cannot describe.</summary>
    public IReadOnlyList<Keypoint> Keypoints { get; }

    /// <summary>Values per descriptor: floats for <see cref="DescriptorKind.Real"/>, bytes for <see cref="DescriptorKind.Binary"/>.</summary>
    public int Dimension { get; }

    /// <summary>Number of descriptors.</summary>
    public int Count => Keypoints.Count;

    /// <summary>Creates a float descriptor set; <paramref name="values"/> holds <paramref name="dimension"/> values per keypoint.</summary>
    public static DescriptorSet FromFloats(IReadOnlyList<Keypoint> keypoints, int dimension, float[] values)
    {
        Validate(keypoints, dimension, values.Length);
        return new DescriptorSet(keypoints, dimension, values, null);
    }

    /// <summary>Creates a binary descriptor set; <paramref name="values"/> holds <paramref name="dimension"/> bytes per keypoint.</summary>
    public static DescriptorSet FromBytes(IReadOnlyList<Keypoint> keypoints, int dimension, byte[] values)
    {
        Validate(keypoints, dimension, values.Length);
        return new DescriptorSet(keypoints, dimension, null, values);
    }

    /// <summary>Real-valued descriptor <paramref name="index"/>.</summary>
    public ReadOnlySpan<float> Real(int index) =>
        (_floats ?? throw new InvalidOperationException("This descriptor set is binary.")).AsSpan(index * Dimension, Dimension);

    /// <summary>Binary descriptor <paramref name="index"/>.</summary>
    public ReadOnlySpan<byte> Binary(int index) =>
        (_bytes ?? throw new InvalidOperationException("This descriptor set is real-valued.")).AsSpan(index * Dimension, Dimension);

    private static void Validate(IReadOnlyList<Keypoint> keypoints, int dimension, int length)
    {
        ArgumentNullException.ThrowIfNull(keypoints);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimension);
        if (length != keypoints.Count * dimension)
        {
            throw new ArgumentException($"Expected {keypoints.Count} × {dimension} values, got {length}.");
        }
    }
}

/// <summary>Computes descriptors for keypoints.</summary>
/// <typeparam name="TParameters">Immutable parameter record (ADR-0003).</typeparam>
public interface IDescriptorExtractor<in TParameters> : IFeatureAlgorithm
    where TParameters : class
{
    /// <summary>Computes one descriptor per keypoint.</summary>
    /// <param name="image">Grayscale image with intensities in [0, 1].</param>
    /// <param name="keypoints">Keypoints in input-image coordinates.</param>
    /// <param name="parameters">Algorithm parameters.</param>
    /// <param name="cancellationToken">Cancels the computation.</param>
    DescriptorSet Compute(ImageF image, IReadOnlyList<Keypoint> keypoints, TParameters parameters, CancellationToken cancellationToken = default);
}
