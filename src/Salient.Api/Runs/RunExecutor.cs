using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Salient.Api.Algorithms;
using Salient.Api.Images;
using Salient.Api.Storage;
using Salient.Core;
using Salient.Evaluation;
using Salient.Features;
using Salient.Features.Pipeline;
using Salient.Features.Sift;
using Salient.Matching;
using Salient.Reference.OpenCv;

namespace Salient.Api.Runs;

/// <summary>Executes the four kinds of runs and stores their results.</summary>
public sealed class RunExecutor(AlgorithmRegistry registry, ImageService images, BlobStore blobs, RunTracker tracker)
{
    /// <summary>Candidate matches up to this ratio are returned, so the UI's ratio slider can go that far (ADR-0006).</summary>
    public const double MaxReturnedRatio = 0.95;

    public static string DetectionPath(Guid id) => $"runs/{id:N}/detection.json.br";

    public static string DescriptorsPath(Guid id) => $"runs/{id:N}/descriptors.bin";

    public static string HistogramsPath(Guid id) => $"runs/{id:N}/histograms.bin";

    public static string MatchingPath(Guid id) => $"runs/{id:N}/matching.json.br";

    public static string BenchmarkPath(Guid id) => $"runs/{id:N}/benchmark.json.br";

    public static string ComparisonPath(Guid id) => $"runs/{id:N}/comparison.json.br";

    public Task<RunObserver> ExecuteAsync(RunRecord run, CancellationToken cancellationToken) => run.Kind switch
    {
        RunKind.Detection => DetectAsync(run, cancellationToken),
        RunKind.Matching => MatchAsync(run, cancellationToken),
        RunKind.Benchmark => BenchmarkAsync(run, cancellationToken),
        RunKind.Comparison => CompareAsync(run, cancellationToken),
        _ => throw new InvalidOperationException($"Unknown run kind {run.Kind}."),
    };

    /// <summary>
    /// Exploration run (ADR-0003): permissive thresholds, diagnostics, descriptors and full pipeline artifacts, so the Detector
    /// and Pipeline views can both be served from it.
    /// </summary>
    private async Task<RunObserver> DetectAsync(RunRecord run, CancellationToken cancellationToken)
    {
        var request = Deserialize<DetectionRequest>(run);
        var observer = Observer(run, PipelineDetail.Full);
        var (image, gray) = await LoadAsync(request.ImageId, observer, "load", cancellationToken);
        var algorithm = Algorithm(request.AlgorithmId);
        var parameters = algorithm.Parse(request.Parameters);
        var exploration = algorithm.Permissive(parameters);

        var output = algorithm.Run(gray, exploration, DetectionOptions.WithDiagnostics, observer, cancellationToken);
        var detection = output.Detection;
        var diagnostics = detection.Diagnostics;
        var keypoints = detection.Keypoints.Select((k, i) => KeypointDto.From(k, diagnostics?[i])).ToList();

        if (output.Descriptors is { } descriptors)
        {
            blobs.Write(DescriptorsPath(run.Id), DescriptorBytes(descriptors));
        }

        if (diagnostics is not null && diagnostics.All(d => d.OrientationHistogram is not null))
        {
            var histograms = diagnostics.SelectMany(d => d.OrientationHistogram!).ToArray();
            blobs.Write(HistogramsPath(run.Id), MemoryMarshal.AsBytes(histograms.AsSpan()));
        }

        blobs.WriteJson(DetectionPath(run.Id), new DetectionResultDto(
            image.Width,
            image.Height,
            JsonSerializer.SerializeToElement(parameters, parameters.GetType(), ApiJson.Options),
            JsonSerializer.SerializeToElement(exploration, exploration.GetType(), ApiJson.Options),
            output.Descriptors is { } d ? new DescriptorInfo(d.Kind, d.Dimension) : null,
            keypoints), ApiJson.Options);
        return observer;
    }

    private async Task<RunObserver> MatchAsync(RunRecord run, CancellationToken cancellationToken)
    {
        var request = Deserialize<MatchingRequest>(run);
        var observer = Observer(run, PipelineDetail.Progress);
        var (imageA, grayA) = await LoadAsync(request.ImageA, observer, "load-a", cancellationToken);
        var (imageB, grayB) = await LoadAsync(request.ImageB, observer, "load-b", cancellationToken);
        var algorithm = Algorithm(request.AlgorithmId);
        var parameters = algorithm.Parse(request.Parameters);

        observer.StagePrefix = "a:";
        var a = algorithm.Run(grayA, parameters, DetectionOptions.Default, observer, cancellationToken).Descriptors!;
        observer.StagePrefix = "b:";
        var b = algorithm.Run(grayB, parameters, DetectionOptions.Default, observer, cancellationToken).Descriptors!;
        observer.StagePrefix = string.Empty;

        var candidates = observer.Stage(
            "matching",
            () => BruteForceMatcher.NearestTwo(a, b, cancellationToken).Where(m => m.Ratio <= MaxReturnedRatio).ToList(),
            m => (a.Count, m.Count, a.Count - m.Count));

        var estimate = observer.Stage(
            "ransac",
            () =>
            {
                var strict = candidates.Where(m => m.Ratio < request.Ratio).ToList();
                return RansacHomography.Estimate(
                    strict.Select(m => Position(a.Keypoints[m.QueryIndex])).ToList(),
                    strict.Select(m => Position(b.Keypoints[m.TrainIndex])).ToList(),
                    new RansacOptions { Threshold = request.RansacThreshold },
                    cancellationToken);
            },
            e => (candidates.Count, e?.InlierCount ?? 0, candidates.Count - (e?.InlierCount ?? 0)));

        // Every returned candidate is classified against the model, so inlier flags stay valid when the client moves the ratio slider.
        var homography = estimate?.Homography;
        var matches = candidates.Select(m => new MatchDto(
            m.QueryIndex,
            m.TrainIndex,
            m.Distance,
            m.Ratio,
            homography is not null && homography.Map(Position(a.Keypoints[m.QueryIndex])).DistanceTo(Position(b.Keypoints[m.TrainIndex])) < request.RansacThreshold)).ToList();

        blobs.WriteJson(MatchingPath(run.Id), new MatchingResultDto(
            imageA.Width,
            imageA.Height,
            imageB.Width,
            imageB.Height,
            a.Keypoints.Select(Point).ToList(),
            b.Keypoints.Select(Point).ToList(),
            matches,
            request.Ratio,
            request.RansacThreshold,
            homography?.ToRowMajor(),
            homography?.RotationDegrees,
            homography?.Scale,
            estimate?.InlierCount ?? 0), ApiJson.Options);
        return observer;
    }

    private async Task<RunObserver> BenchmarkAsync(RunRecord run, CancellationToken cancellationToken)
    {
        var request = Deserialize<BenchmarkRequest>(run);
        var observer = Observer(run, PipelineDetail.Progress);
        var (_, gray) = await LoadAsync(request.ImageId, observer, "load", cancellationToken);
        var extractors = request.AlgorithmIds.Select(id =>
        {
            var algorithm = Algorithm(id);
            var defaults = algorithm.Parse(null);
            return new FeatureExtractor(
                algorithm.Descriptor.Id,
                algorithm.Descriptor.DisplayName,
                (img, ct) => algorithm.Run(img, defaults, DetectionOptions.Default, null, ct).Descriptors!);
        }).ToList();
        var progress = new InlineProgress<SweepProgress>(p =>
            tracker.Progress(run.Id, new RunProgress(p.Completed, p.Total, $"{p.Family} {p.Value} · {p.ExtractorId}")));

        var results = observer.Stage(
            "sweep",
            () => RobustnessSweep.Run(gray, request.Families!, extractors, progress: progress, cancellationToken: cancellationToken),
            r => (0, r.Count, 0));

        blobs.WriteJson(BenchmarkPath(run.Id), new BenchmarkResultDto(results), ApiJson.Options);
        return observer;
    }

    private async Task<RunObserver> CompareAsync(RunRecord run, CancellationToken cancellationToken)
    {
        var request = Deserialize<ComparisonRequest>(run);
        var observer = Observer(run, PipelineDetail.Progress);
        var (_, gray) = await LoadAsync(request.ImageId, observer, "load", cancellationToken);

        // OpenCV always doubles the input, so Salient runs with upsampling too for a like-for-like comparison.
        var parameters = (SiftParameters)Algorithm("sift").Parse(request.Parameters) with { UpsampleInput = true };
        var reference = new OpenCvSiftParameters
        {
            LayersPerOctave = parameters.LayersPerOctave,
            ContrastThreshold = parameters.ContrastThreshold,
            EdgeThreshold = parameters.EdgeThreshold,
            Sigma = parameters.Sigma,
        };

        var (ours, oursMs) = observer.Stage("salient", () => Timed(() => new SiftDetector().Detect(gray, parameters, cancellationToken: cancellationToken).Keypoints), r => (0, r.Value.Count, 0));
        var (theirs, theirsMs) = observer.Stage("opencv", () => Timed(() => new OpenCvSiftDetector().Detect(gray, reference, cancellationToken: cancellationToken).Keypoints), r => (0, r.Value.Count, 0));

        var result = observer.Stage("compare", () => Compare(ours, theirs, oursMs, theirsMs), _ => (0, 0, 0));
        blobs.WriteJson(ComparisonPath(run.Id), result, ApiJson.Options);
        return observer;
    }

    private static ComparisonResultDto Compare(IReadOnlyList<Keypoint> ours, IReadOnlyList<Keypoint> theirs, double oursMs, double theirsMs)
    {
        static bool Same(Keypoint p, Keypoint q) =>
            Math.Abs(p.X - q.X) < 2 && Math.Abs(p.Y - q.Y) < 2 &&
            (((p.X - q.X) * (p.X - q.X)) + ((p.Y - q.Y) * (p.Y - q.Y))) < 4 &&
            Math.Abs((q.Scale / p.Scale) - 1) < 0.2;

        var grid = ours.GroupBy(k => ((int)(k.X / 4), (int)(k.Y / 4))).ToDictionary(g => g.Key, g => g.ToList());
        IEnumerable<Keypoint> Near(Keypoint k)
        {
            var (cx, cy) = ((int)(k.X / 4), (int)(k.Y / 4));
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (grid.TryGetValue((cx + dx, cy + dy), out var list))
                    {
                        foreach (var p in list)
                        {
                            yield return p;
                        }
                    }
                }
            }
        }

        var offsets = new List<double>();
        var pairedOurs = new HashSet<Keypoint>();
        foreach (var t in theirs)
        {
            var match = Near(t).Where(o => Same(o, t)).OrderBy(o => ((o.X - t.X) * (o.X - t.X)) + ((o.Y - t.Y) * (o.Y - t.Y))).FirstOrDefault();
            if (Same(match, t))
            {
                offsets.Add(Math.Sqrt(((match.X - t.X) * (match.X - t.X)) + ((match.Y - t.Y) * (match.Y - t.Y))));
                pairedOurs.Add(match);
            }
        }

        return new ComparisonResultDto(
            ours.Select(Point).ToList(),
            theirs.Select(Point).ToList(),
            oursMs,
            theirsMs,
            theirs.Count == 0 ? 0 : offsets.Count / (double)theirs.Count,
            ours.Count(o => !pairedOurs.Contains(o)),
            theirs.Count - offsets.Count,
            offsets.Count == 0 ? 0 : offsets.Average());
    }

    private async Task<(ImageInfo Image, ImageF Gray)> LoadAsync(string imageId, RunObserver observer, string stageId, CancellationToken cancellationToken)
    {
        observer.StageStarted(stageId);
        var started = Stopwatch.GetTimestamp();
        var image = await images.FindAsync(imageId, cancellationToken)
            ?? throw new RunRequestException(StatusCodes.Status404NotFound, $"Image '{imageId}' was not found.");
        var gray = await images.LoadGrayAsync(image, cancellationToken);
        observer.StageCompleted(stageId, Stopwatch.GetElapsedTime(started), StageSummary.None);
        return (image, gray);
    }

    private RunObserver Observer(RunRecord run, PipelineDetail detail) => new(run.Id, detail, tracker, blobs, ApiJson.Options);

    private AlgorithmRegistration Algorithm(string id) =>
        registry.Find(id) ?? throw new RunRequestException(StatusCodes.Status404NotFound, $"Algorithm '{id}' is not available.");

    private static T Deserialize<T>(RunRecord run) =>
        JsonSerializer.Deserialize<T>(run.RequestJson, ApiJson.Options) ?? throw new InvalidOperationException("Empty run request.");

    private static byte[] DescriptorBytes(DescriptorSet set)
    {
        if (set.Kind == DescriptorKind.Binary)
        {
            var bytes = new byte[set.Count * set.Dimension];
            for (var i = 0; i < set.Count; i++)
            {
                set.Binary(i).CopyTo(bytes.AsSpan(i * set.Dimension));
            }

            return bytes;
        }

        var floats = new float[set.Count * set.Dimension];
        for (var i = 0; i < set.Count; i++)
        {
            set.Real(i).CopyTo(floats.AsSpan(i * set.Dimension));
        }

        return MemoryMarshal.AsBytes(floats.AsSpan()).ToArray();
    }

    private static (T Value, double Milliseconds) Timed<T>(Func<T> work)
    {
        var started = Stopwatch.GetTimestamp();
        var value = work();
        return (value, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    private static Point2 Position(Keypoint k) => new(k.X, k.Y);

    private static PointDto Point(Keypoint k) => new(k.X, k.Y, k.Scale, k.Angle);

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
