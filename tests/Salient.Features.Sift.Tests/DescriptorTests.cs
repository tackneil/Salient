namespace Salient.Features.Sift.Tests;

public sealed class DescriptorTests
{
    private readonly SiftDetector _detector = new();

    [Fact]
    public void Descriptors_are_unit_length_128_vectors_aligned_with_keypoints()
    {
        var image = SyntheticImages.Texture(128, 96, seed: 9);

        var (detection, descriptors) = _detector.DetectAndCompute(image, new SiftParameters());

        Assert.Equal(DescriptorKind.Real, descriptors.Kind);
        Assert.Equal(128, descriptors.Dimension);
        Assert.Equal(detection.Keypoints, descriptors.Keypoints);
        for (var i = 0; i < descriptors.Count; i++)
        {
            var d = descriptors.Real(i);
            double norm = 0;
            foreach (var v in d)
            {
                Assert.True(v >= 0);
                norm += v * v;
            }

            Assert.Equal(1.0, Math.Sqrt(norm), 4);
        }
    }

    [Fact]
    public void Compute_on_detected_keypoints_equals_DetectAndCompute()
    {
        var image = SyntheticImages.Texture(128, 96, seed: 11);
        var parameters = new SiftParameters();

        var (detection, combined) = _detector.DetectAndCompute(image, parameters);
        var separate = _detector.Compute(image, detection.Keypoints, parameters);

        Assert.Equal(combined.Count, separate.Count);
        for (var i = 0; i < combined.Count; i++)
        {
            Assert.Equal(combined.Real(i).ToArray(), separate.Real(i).ToArray());
        }
    }

    [Fact]
    public void Descriptor_is_invariant_to_a_90_degree_rotation()
    {
        var image = SyntheticImages.Blob(129, 64, 64, sigma: 6);
        SyntheticImages.AddBlob(image, 71, 62, sigma: 2.5, amplitude: 0.35f);
        SyntheticImages.AddBlob(image, 60, 70, sigma: 2, amplitude: -0.2f);
        var rotated = SyntheticImages.RotateCounterclockwise90(image);

        var a = Strongest(_detector.DetectAndCompute(image, new SiftParameters()).Descriptors);
        var b = Strongest(_detector.DetectAndCompute(rotated, new SiftParameters()).Descriptors);

        double dot = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
        }

        Assert.True(dot > 0.95, $"Cosine similarity {dot:F3}.");
    }

    [Fact]
    public void Descriptor_set_rejects_mismatched_sizes_and_wrong_kind_access()
    {
        Keypoint[] keypoints = [new(1, 2, 3, 0, 1, 0, 1)];

        Assert.Throws<ArgumentException>(() => DescriptorSet.FromFloats(keypoints, 128, new float[127]));
        var binary = DescriptorSet.FromBytes(keypoints, 32, new byte[32]);
        Assert.Throws<InvalidOperationException>(() => binary.Real(0).Length);
    }

    private static float[] Strongest(DescriptorSet set)
    {
        var best = Enumerable.Range(0, set.Count)
            .Where(i => Math.Abs(set.Keypoints[i].X - 64) < 3 && Math.Abs(set.Keypoints[i].Y - 64) < 3)
            .MaxBy(i => set.Keypoints[i].Response);
        return set.Real(best).ToArray();
    }
}
