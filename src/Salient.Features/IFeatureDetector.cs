using Salient.Core;
using Salient.Features.Pipeline;

namespace Salient.Features;

/// <summary>Common base of all feature algorithms.</summary>
public interface IFeatureAlgorithm
{
    /// <summary>Static description of the algorithm.</summary>
    AlgorithmDescriptor Descriptor { get; }
}

/// <summary>Detects keypoints in a grayscale image.</summary>
/// <typeparam name="TParameters">Immutable parameter record (ADR-0003).</typeparam>
public interface IFeatureDetector<in TParameters> : IFeatureAlgorithm
    where TParameters : class
{
    /// <summary>Detects keypoints.</summary>
    /// <param name="image">Grayscale image with intensities in [0, 1].</param>
    /// <param name="parameters">Algorithm parameters.</param>
    /// <param name="options">What to return besides keypoints; <see langword="null"/> means <see cref="DetectionOptions.Default"/>.</param>
    /// <param name="observer">Optional observer for stage progress and intermediate results (ADR-0004).</param>
    /// <param name="cancellationToken">Cancels the detection between stages.</param>
    DetectionResult Detect(
        ImageF image,
        TParameters parameters,
        DetectionOptions? options = null,
        IPipelineObserver? observer = null,
        CancellationToken cancellationToken = default);
}
