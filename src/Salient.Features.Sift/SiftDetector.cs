using Salient.Core;
using Salient.Features.Pipeline;

namespace Salient.Features.Sift;

/// <summary>Salient's own SIFT detector and descriptor (Lowe 2004). Each stage is a separate class so the UI can link to it.</summary>
public sealed class SiftDetector : IFeatureDetector<SiftParameters>, IDescriptorExtractor<SiftParameters>
{
    /// <summary>Output version; bump whenever results change (ADR-0008).</summary>
    public const int Version = 1;

    /// <inheritdoc />
    public AlgorithmDescriptor Descriptor { get; } = new(
        "sift",
        "SIFT · Salient",
        "SIFT",
        AlgorithmCapabilities.Detects | AlgorithmCapabilities.Describes | AlgorithmCapabilities.ExposesPipeline,
        typeof(SiftParameters),
        Version);

    /// <inheritdoc />
    public DetectionResult Detect(
        ImageF image,
        SiftParameters parameters,
        DetectionOptions? options = null,
        IPipelineObserver? observer = null,
        CancellationToken cancellationToken = default)
    {
        var reporter = new PipelineReporter(observer);
        return Detect(image, parameters, options, reporter, cancellationToken).Result;
    }

    /// <summary>
    /// Detects keypoints and computes their descriptors in one pass, reusing the Gaussian pyramid.
    /// Reports the <see cref="SiftStages.Descriptor"/> stage after detection.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Part of the detector's instance API, next to the interface methods.")]
    public (DetectionResult Detection, DescriptorSet Descriptors) DetectAndCompute(
        ImageF image,
        SiftParameters parameters,
        DetectionOptions? options = null,
        IPipelineObserver? observer = null,
        CancellationToken cancellationToken = default)
    {
        var reporter = new PipelineReporter(observer);
        var (result, scaleSpace) = Detect(image, parameters, options, reporter, cancellationToken);
        var descriptors = reporter.Run(
            SiftStages.Descriptor,
            () => DescriptorSet.FromFloats(
                result.Keypoints,
                DescriptorBuilder.Dimension,
                DescriptorBuilder.ComputeAll(scaleSpace, result.Keypoints, cancellationToken)),
            d => new StageSummary(result.Keypoints.Count, d.Count, 0));
        return (result, descriptors);
    }

    /// <summary>
    /// Computes descriptors for keypoints, which should come from this detector run with the same scale-space parameters
    /// (σ₀, layers, upsampling). Keypoints from other detectors are described at the pyramid layer nearest to their scale.
    /// Descriptors have unit length.
    /// </summary>
    public DescriptorSet Compute(ImageF image, IReadOnlyList<Keypoint> keypoints, SiftParameters parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(keypoints);
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();
        var scaleSpace = GaussianPyramid.Build(image, parameters, cancellationToken);
        return DescriptorSet.FromFloats(keypoints, DescriptorBuilder.Dimension, DescriptorBuilder.ComputeAll(scaleSpace, keypoints, cancellationToken));
    }

    private static (DetectionResult Result, ScaleSpace ScaleSpace) Detect(
        ImageF image,
        SiftParameters parameters,
        DetectionOptions? options,
        PipelineReporter reporter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Validate();
        options ??= DetectionOptions.Default;

        var scaleSpace = reporter.Run(
            SiftStages.GaussianPyramid,
            () => GaussianPyramid.Build(image, parameters, cancellationToken),
            _ => StageSummary.None);
        ReportLayers(reporter, scaleSpace, SiftStages.GaussianPyramid, ImageLayerKind.Gaussian);

        reporter.Run(
            SiftStages.DifferenceOfGaussians,
            () =>
            {
                DogPyramid.Build(scaleSpace, cancellationToken);
                return scaleSpace;
            },
            _ => StageSummary.None);
        ReportLayers(reporter, scaleSpace, SiftStages.DifferenceOfGaussians, ImageLayerKind.DifferenceOfGaussians);

        var extrema = reporter.Run(
            SiftStages.Extrema,
            () => ExtremaDetector.Find(scaleSpace, SiftParameters.MinContrastThreshold, cancellationToken),
            found => new StageSummary(found.Count, found.Count, 0));
        reporter.Artifact(SiftStages.Extrema, () => new CandidateSetArtifact(
            extrema.ConvertAll(p => Candidate(scaleSpace, p, RejectionReason.None))));

        var (refined, unstable) = reporter.Run(
            SiftStages.Refine,
            () => KeypointRefiner.RefineAll(scaleSpace, extrema, cancellationToken),
            r => new StageSummary(extrema.Count, r.Kept.Count, r.Rejected.Count));
        reporter.Artifact(SiftStages.Refine, () => new CandidateSetArtifact(
            [.. refined.Select(p => Candidate(scaleSpace, p, RejectionReason.None)),
             .. unstable.Select(p => Candidate(scaleSpace, p, RejectionReason.Unstable))]));

        var (contrasted, lowContrast) = reporter.Run(
            SiftStages.Contrast,
            () => ContrastFilter.Apply(refined, parameters.ContrastThreshold),
            r => new StageSummary(refined.Count, r.Kept.Count, r.Rejected.Count));
        reporter.Artifact(SiftStages.Contrast, () => new CandidateSetArtifact(
            [.. contrasted.Select(p => Candidate(scaleSpace, p, RejectionReason.None)),
             .. lowContrast.Select(p => Candidate(scaleSpace, p, RejectionReason.LowContrast))]));

        var (stable, onEdges) = reporter.Run(
            SiftStages.Edge,
            () => EdgeFilter.Apply(scaleSpace, contrasted, parameters.EdgeThreshold),
            r => new StageSummary(contrasted.Count, r.Kept.Count, r.Rejected.Count));
        reporter.Artifact(SiftStages.Edge, () => new CandidateSetArtifact(
            [.. stable.Select(p => Candidate(scaleSpace, p, RejectionReason.None)),
             .. onEdges.Select(p => Candidate(scaleSpace, p, RejectionReason.EdgeResponse))]));

        var oriented = reporter.Run(
            SiftStages.Orientation,
            () => OrientationAssigner.AssignAll(scaleSpace, stable, cancellationToken),
            o => new StageSummary(stable.Count, o.Count, 0));

        if (reporter.WantsArtifacts)
        {
            var heatmaps = reporter.Run(
                SiftStages.Heatmaps,
                () => HeatmapBuilder.Build(scaleSpace),
                _ => StageSummary.None);
            foreach (var heatmap in heatmaps)
            {
                reporter.Artifact(SiftStages.Heatmaps, () => heatmap);
            }
        }

        var keypoints = oriented.ConvertAll(o => o.Keypoint);
        var diagnostics = options.IncludeDiagnostics
            ? oriented.ConvertAll(o => new KeypointDiagnostics(o.Source.Contrast, o.Source.EdgeRatio, o.Histogram))
            : null;
        return (new DetectionResult(keypoints, diagnostics), scaleSpace);
    }

    private static void ReportLayers(PipelineReporter reporter, ScaleSpace scaleSpace, string stage, ImageLayerKind kind)
    {
        if (!reporter.WantsArtifacts)
        {
            return;
        }

        foreach (var octave in scaleSpace.Octaves)
        {
            var layers = kind == ImageLayerKind.Gaussian ? octave.Gaussians : octave.Dogs;
            for (var i = 0; i < layers.Length; i++)
            {
                var layer = i;
                reporter.Artifact(stage, () => new ImageLayerArtifact(
                    octave.Index, layer, kind, scaleSpace.SigmaAt(octave, layer), layers[layer]));
            }
        }
    }

    private static PipelineCandidate Candidate(ScaleSpace scaleSpace, ScaleSpacePoint point, RejectionReason reason)
    {
        var octave = scaleSpace.Octaves[point.Octave];
        return new PipelineCandidate(
            point.X * octave.PixelSize,
            point.Y * octave.PixelSize,
            scaleSpace.SigmaAt(octave, point.Layer),
            octave.Index,
            float.NaN,
            float.NaN,
            reason);
    }

    private static PipelineCandidate Candidate(ScaleSpace scaleSpace, RefinedPoint point, RejectionReason reason)
    {
        var octave = scaleSpace.Octaves[point.Location.Octave];
        return new PipelineCandidate(
            (point.Location.X + point.OffsetX) * octave.PixelSize,
            (point.Location.Y + point.OffsetY) * octave.PixelSize,
            scaleSpace.SigmaAt(octave, point.Location.Layer + point.OffsetLayer),
            octave.Index,
            point.Contrast,
            point.EdgeRatio,
            reason);
    }
}
