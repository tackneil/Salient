using System.Text.Json;
using Salient.Api.Rendering;
using Salient.Evaluation;
using Salient.Features;

namespace Salient.Api.Runs;

/// <summary>What a run computes.</summary>
public enum RunKind
{
    /// <summary>Keypoints, diagnostics, descriptors and pipeline artifacts for one image.</summary>
    Detection,

    /// <summary>Matches and a homography between two images.</summary>
    Matching,

    /// <summary>A robustness sweep over transformation families.</summary>
    Benchmark,

    /// <summary>Salient's SIFT against OpenCV's on one image.</summary>
    Comparison,
}

/// <summary>Lifecycle of a run (ADR-0005).</summary>
public enum RunStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Canceled,
}

/// <summary>State of a stage in <see cref="StageInfo"/>.</summary>
public enum StageState
{
    Running,
    Completed,
}

// ----- Requests -----

/// <summary>Detect keypoints in an image (Detector and Pipeline views).</summary>
/// <param name="ImageId">Uploaded image.</param>
/// <param name="AlgorithmId">Registered algorithm, for example <c>sift</c>.</param>
/// <param name="Parameters">Algorithm parameters; omitted values use defaults.</param>
public sealed record DetectionRequest(string ImageId, string AlgorithmId, JsonElement? Parameters);

/// <summary>Match two images (Matching view).</summary>
/// <param name="ImageA">First image.</param>
/// <param name="ImageB">Second image.</param>
/// <param name="AlgorithmId">A registered algorithm that computes descriptors.</param>
/// <param name="Parameters">Algorithm parameters.</param>
/// <param name="Ratio">Ratio-test threshold for the matches RANSAC is fit on; all candidates up to 0.95 are returned.</param>
/// <param name="RansacThreshold">Reprojection threshold in pixels.</param>
public sealed record MatchingRequest(string ImageA, string ImageB, string AlgorithmId, JsonElement? Parameters, double Ratio = 0.8, double RansacThreshold = 3.0);

/// <summary>Run a robustness sweep (Robustness view).</summary>
/// <param name="ImageId">Reference image.</param>
/// <param name="AlgorithmIds">Algorithms to compare; each must compute descriptors.</param>
/// <param name="Families">Families and values to sweep; defaults to <see cref="Transformations.DefaultValues"/>.</param>
public sealed record BenchmarkRequest(string ImageId, IReadOnlyList<string> AlgorithmIds, Dictionary<TransformationFamily, double[]>? Families);

/// <summary>Compare Salient's SIFT with OpenCV's on one image (Compare dialog).</summary>
/// <param name="ImageId">Image.</param>
/// <param name="Parameters">Salient SIFT parameters; the OpenCV run uses the equivalent values.</param>
public sealed record ComparisonRequest(string ImageId, JsonElement? Parameters);

// ----- Run status -----

/// <summary>Response to a run creation.</summary>
/// <param name="RunId">Run identifier; subscribe to it on the hub.</param>
/// <param name="Status">Current status; <see cref="RunStatus.Completed"/> for cached results.</param>
/// <param name="Cached">Whether an earlier identical run was reused (ADR-0008).</param>
public sealed record RunCreated(Guid RunId, RunStatus Status, bool Cached);

/// <summary>A stage of a run, live or finished.</summary>
public sealed record StageInfo(string Id, StageState State, double? DurationMs, int Input, int Kept, int Rejected);

/// <summary>Sweep progress for benchmark runs.</summary>
public sealed record RunProgress(int Completed, int Total, string? Message);

/// <summary>A rendered image layer of a run.</summary>
/// <param name="Id">Layer identifier used in <c>GET /api/runs/{id}/layers/{layerId}</c>.</param>
/// <param name="Kind"><c>gaussian</c>, <c>dog</c> or <c>heatmap</c>.</param>
/// <param name="Name">For heatmaps the heatmap kind; otherwise null.</param>
/// <param name="Octave">Octave number, or null for heatmaps.</param>
/// <param name="Layer">Layer within the octave, or null for heatmaps.</param>
/// <param name="Sigma">σ in input-image pixels.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Legend">Value range and colormap.</param>
public sealed record LayerInfo(string Id, string Kind, string? Name, int? Octave, int? Layer, double Sigma, int Width, int Height, LayerLegend Legend);

/// <summary>Everything known about a run.</summary>
public sealed record RunInfo(
    Guid Id,
    RunKind Kind,
    RunStatus Status,
    string AlgorithmId,
    string ImageId,
    string? SecondImageId,
    DateTime CreatedAt,
    double? DurationMs,
    string? Error,
    IReadOnlyList<StageInfo> Stages,
    IReadOnlyList<LayerInfo> Layers,
    RunProgress? Progress);

// ----- Results -----

/// <summary>A keypoint as sent to the UI. <paramref name="EdgeRatio"/> is null when it is infinite (det(H) ≤ 0).</summary>
public sealed record KeypointDto(float X, float Y, float Scale, float Angle, float Response, int Octave, float? Contrast, float? EdgeRatio)
{
    public static KeypointDto From(Keypoint k, KeypointDiagnostics? d) => new(
        k.X, k.Y, k.Scale, k.Angle, k.Response, k.Octave, d?.Contrast,
        d?.EdgeRatio is { } r && float.IsFinite(r) ? r : null);
}

/// <summary>Detection output. Interactive runs use permissive thresholds; the client filters with <paramref name="Parameters"/>.</summary>
/// <param name="Width">Image width.</param>
/// <param name="Height">Image height.</param>
/// <param name="Parameters">The parameters as requested; their thresholds are the initial slider values.</param>
/// <param name="ExplorationParameters">The permissive parameters the run actually used.</param>
/// <param name="Descriptor">Descriptor kind and size, or null when the algorithm does not describe.</param>
/// <param name="Keypoints">All keypoints of the exploration run.</param>
public sealed record DetectionResultDto(
    int Width,
    int Height,
    JsonElement Parameters,
    JsonElement ExplorationParameters,
    DescriptorInfo? Descriptor,
    IReadOnlyList<KeypointDto> Keypoints);

/// <summary>Descriptor format.</summary>
public sealed record DescriptorInfo(DescriptorKind Kind, int Dimension);

/// <summary>One keypoint with everything the inspector shows.</summary>
public sealed record KeypointDetail(int Index, KeypointDto Keypoint, IReadOnlyList<float>? OrientationHistogram, IReadOnlyList<float>? Descriptor, IReadOnlyList<byte>? BinaryDescriptor);

/// <summary>A candidate of a pipeline stage; <paramref name="Rejection"/> is null for kept candidates.</summary>
public sealed record CandidateDto(float X, float Y, float Scale, int Octave, float? Contrast, float? EdgeRatio, string? Rejection);

/// <summary>A position-only keypoint for matching results.</summary>
public sealed record PointDto(float X, float Y, float Scale, float Angle);

/// <summary>A candidate match; <paramref name="Inlier"/> agrees with the estimated homography.</summary>
public sealed record MatchDto(int A, int B, float Distance, float Ratio, bool Inlier);

/// <summary>Matching output.</summary>
public sealed record MatchingResultDto(
    int WidthA,
    int HeightA,
    int WidthB,
    int HeightB,
    IReadOnlyList<PointDto> KeypointsA,
    IReadOnlyList<PointDto> KeypointsB,
    IReadOnlyList<MatchDto> Matches,
    double Ratio,
    double RansacThreshold,
    IReadOnlyList<double>? Homography,
    double? RotationDegrees,
    double? Scale,
    int InlierCount);

/// <summary>Benchmark output.</summary>
public sealed record BenchmarkResultDto(IReadOnlyList<SweepResult> Families);

/// <summary>Comparison output.</summary>
/// <param name="Salient">Salient's keypoints.</param>
/// <param name="Reference">OpenCV's keypoints.</param>
/// <param name="SalientMs">Salient detection time.</param>
/// <param name="ReferenceMs">OpenCV detection time.</param>
/// <param name="Repeatability">Fraction of OpenCV keypoints that Salient also finds (within 2 px and 20% scale).</param>
/// <param name="OnlySalient">Salient keypoints without an OpenCV counterpart.</param>
/// <param name="OnlyReference">OpenCV keypoints without a Salient counterpart.</param>
/// <param name="MeanOffset">Mean distance between paired keypoints, in pixels.</param>
public sealed record ComparisonResultDto(
    IReadOnlyList<PointDto> Salient,
    IReadOnlyList<PointDto> Reference,
    double SalientMs,
    double ReferenceMs,
    double Repeatability,
    int OnlySalient,
    int OnlyReference,
    double MeanOffset);
