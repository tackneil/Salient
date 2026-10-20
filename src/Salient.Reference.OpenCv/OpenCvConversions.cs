using OpenCvSharp;
using Salient.Core;
using Salient.Features;

namespace Salient.Reference.OpenCv;

/// <summary>Conversions between Salient types and OpenCV, applying the conventions of ADR-0003.</summary>
internal static class OpenCvConversions
{
    /// <summary>Copies an image into an 8-bit single-channel <see cref="Mat"/>.</summary>
    public static Mat ToMat(ImageF image) =>
        Mat.FromPixelData(image.Height, image.Width, MatType.CV_8UC1, image.ToGray8());

    /// <summary>
    /// Converts an OpenCV keypoint. OpenCV's size is a diameter (2σ for SIFT) and its angle is clockwise on screen,
    /// or −1 when not computed.
    /// </summary>
    public static Keypoint ToKeypoint(KeyPoint point, bool packedSiftOctave)
    {
        var (octave, layer) = packedSiftOctave ? UnpackSiftOctave(point.Octave) : (point.Octave, 0);
        var angle = point.Angle < 0 ? 0f : (360f - point.Angle) % 360f;
        return new Keypoint(point.Pt.X, point.Pt.Y, point.Size / 2, angle, point.Response, octave, layer);
    }

    /// <summary>Converts back to an OpenCV keypoint, for example to compute descriptors.</summary>
    public static KeyPoint ToOpenCv(Keypoint point, bool packedSiftOctave)
    {
        var octave = packedSiftOctave ? (point.Octave & 255) | ((point.Layer & 255) << 8) : point.Octave;
        return new KeyPoint(point.X, point.Y, point.Scale * 2, (360f - point.Angle) % 360f, point.Response, octave);
    }

    /// <summary>OpenCV's SIFT packs the octave (signed byte) and layer into one integer.</summary>
    public static (int Octave, int Layer) UnpackSiftOctave(int packed)
    {
        var octave = packed & 255;
        if (octave >= 128)
        {
            octave -= 256;
        }

        return (octave, (packed >> 8) & 255);
    }

    /// <summary>
    /// Reads descriptors from a <see cref="Mat"/>. Float descriptors are scaled to unit length to match Salient's SIFT
    /// (OpenCV stores them scaled to about 512); this does not change nearest-neighbour matching.
    /// </summary>
    public static DescriptorSet ToDescriptorSet(Mat descriptors, IReadOnlyList<Keypoint> keypoints)
    {
        if (keypoints.Count == 0 || descriptors.Empty())
        {
            return DescriptorSet.FromBytes([], 1, []);
        }

        var dimension = descriptors.Cols;
        if (descriptors.Type() == MatType.CV_32FC1)
        {
            descriptors.GetArray(out float[] values);
            for (var row = 0; row < keypoints.Count; row++)
            {
                var span = values.AsSpan(row * dimension, dimension);
                double sum = 0;
                foreach (var v in span)
                {
                    sum += v * v;
                }

                var scale = (float)(1 / Math.Max(Math.Sqrt(sum), 1e-12));
                for (var i = 0; i < span.Length; i++)
                {
                    span[i] *= scale;
                }
            }

            return DescriptorSet.FromFloats(keypoints, dimension, values);
        }

        descriptors.GetArray(out byte[] bytes);
        return DescriptorSet.FromBytes(keypoints, dimension, bytes);
    }
}
