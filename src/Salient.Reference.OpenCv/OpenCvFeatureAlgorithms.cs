using OpenCvSharp;
using OpenCvSharp.Features2D;
using Salient.Core;
using Salient.Features;
using Salient.Features.Pipeline;

namespace Salient.Reference.OpenCv;

/// <summary>Whether the native OpenCV library can be loaded on this machine (ADR-0013).</summary>
public static class OpenCvAvailability
{
    private static readonly Lazy<string?> s_version = new(() =>
    {
        try
        {
            return Cv2.GetVersionString();
        }
#pragma warning disable CA1031 // Any load failure means "not available".
        catch (Exception)
#pragma warning restore CA1031
        {
            return null;
        }
    });

    /// <summary><see langword="true"/> when OpenCV's native library loads.</summary>
    public static bool IsAvailable => s_version.Value is not null;

    /// <summary>OpenCV version string, or <see langword="null"/> when unavailable.</summary>
    public static string? Version => s_version.Value;
}

/// <summary>Shared plumbing for OpenCV <see cref="Feature2D"/> algorithms. Progress is not reported per stage.</summary>
/// <typeparam name="TParameters">Parameter record.</typeparam>
public abstract class OpenCvFeatureAlgorithm<TParameters> : IFeatureDetector<TParameters>, IDescriptorExtractor<TParameters>
    where TParameters : class
{
    /// <summary>Stage id reported for the whole OpenCV call.</summary>
    public const string StageId = "opencv";

    /// <inheritdoc />
    public abstract AlgorithmDescriptor Descriptor { get; }

    /// <summary>Whether keypoint octaves use SIFT's packed octave/layer format.</summary>
    protected virtual bool PackedSiftOctave => false;

    /// <inheritdoc />
    public DetectionResult Detect(
        ImageF image,
        TParameters parameters,
        DetectionOptions? options = null,
        IPipelineObserver? observer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(parameters);
        cancellationToken.ThrowIfCancellationRequested();
        var reporter = new PipelineReporter(observer);
        var keypoints = reporter.Run(
            StageId,
            () =>
            {
                using var mat = OpenCvConversions.ToMat(image);
                using var algorithm = Create(parameters);
                return algorithm.Detect(mat).Select(k => OpenCvConversions.ToKeypoint(k, PackedSiftOctave)).ToList();
            },
            k => new StageSummary(k.Count, k.Count, 0));
        return new DetectionResult(keypoints, null);
    }

    /// <summary>Detects and describes in one OpenCV call.</summary>
    public (DetectionResult Detection, DescriptorSet Descriptors) DetectAndCompute(
        ImageF image,
        TParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(parameters);
        cancellationToken.ThrowIfCancellationRequested();
        using var mat = OpenCvConversions.ToMat(image);
        using var algorithm = Create(parameters);
        using var descriptors = new Mat();
        algorithm.DetectAndCompute(mat, null, out var cvKeypoints, descriptors);
        var keypoints = cvKeypoints.Select(k => OpenCvConversions.ToKeypoint(k, PackedSiftOctave)).ToList();
        return (new DetectionResult(keypoints, null), OpenCvConversions.ToDescriptorSet(descriptors, keypoints));
    }

    /// <inheritdoc />
    public DescriptorSet Compute(ImageF image, IReadOnlyList<Keypoint> keypoints, TParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(keypoints);
        ArgumentNullException.ThrowIfNull(parameters);
        cancellationToken.ThrowIfCancellationRequested();
        using var mat = OpenCvConversions.ToMat(image);
        using var algorithm = Create(parameters);
        using var descriptors = new Mat();
        var cvKeypoints = keypoints.Select(k => OpenCvConversions.ToOpenCv(k, PackedSiftOctave)).ToArray();
        algorithm.Compute(mat, ref cvKeypoints, descriptors);

        // OpenCV may drop keypoints it cannot describe, so the returned list is authoritative.
        var described = cvKeypoints.Select(k => OpenCvConversions.ToKeypoint(k, PackedSiftOctave)).ToList();
        return OpenCvConversions.ToDescriptorSet(descriptors, described);
    }

    /// <summary>Creates the configured OpenCV algorithm; the caller disposes it.</summary>
    protected abstract Feature2D Create(TParameters parameters);
}

/// <summary>OpenCV's SIFT, used as the reference for Salient's own implementation.</summary>
public sealed class OpenCvSiftDetector : OpenCvFeatureAlgorithm<OpenCvSiftParameters>
{
    /// <inheritdoc />
    public override AlgorithmDescriptor Descriptor { get; } = new(
        "opencv-sift", "SIFT · OpenCV", "SIFT",
        AlgorithmCapabilities.Detects | AlgorithmCapabilities.Describes | AlgorithmCapabilities.Reference,
        typeof(OpenCvSiftParameters), 1);

    /// <inheritdoc />
    protected override bool PackedSiftOctave => true;

    /// <inheritdoc />
    protected override Feature2D Create(OpenCvSiftParameters parameters) =>
        SIFT.Create(parameters.MaxFeatures, parameters.LayersPerOctave, parameters.ContrastThreshold, parameters.EdgeThreshold, parameters.Sigma);
}

/// <summary>OpenCV's ORB: FAST keypoints with binary rBRIEF descriptors.</summary>
public sealed class OrbDetector : OpenCvFeatureAlgorithm<OrbParameters>
{
    /// <inheritdoc />
    public override AlgorithmDescriptor Descriptor { get; } = new(
        "opencv-orb", "ORB · OpenCV", "ORB",
        AlgorithmCapabilities.Detects | AlgorithmCapabilities.Describes | AlgorithmCapabilities.Reference,
        typeof(OrbParameters), 1);

    /// <inheritdoc />
    protected override Feature2D Create(OrbParameters parameters) =>
        ORB.Create(parameters.MaxFeatures, (float)parameters.ScaleFactor, parameters.Levels, fastThreshold: parameters.FastThreshold);
}

/// <summary>OpenCV's AKAZE: nonlinear scale space with binary M-LDB descriptors.</summary>
public sealed class AkazeDetector : OpenCvFeatureAlgorithm<AkazeParameters>
{
    /// <inheritdoc />
    public override AlgorithmDescriptor Descriptor { get; } = new(
        "opencv-akaze", "AKAZE · OpenCV", "AKAZE",
        AlgorithmCapabilities.Detects | AlgorithmCapabilities.Describes | AlgorithmCapabilities.Reference,
        typeof(AkazeParameters), 1);

    /// <inheritdoc />
    protected override Feature2D Create(AkazeParameters parameters) =>
        AKAZE.Create(threshold: (float)parameters.Threshold, nOctaves: parameters.Octaves, nOctaveLayers: parameters.LayersPerOctave);
}
