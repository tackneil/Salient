using System.Text.Json;
using Salient.Api.Rendering;
using Salient.Api.Storage;
using Salient.Core;
using Salient.Features.Pipeline;

namespace Salient.Api.Runs;

/// <summary>
/// Turns pipeline events into live stage updates, stored stage records, rendered layers and candidate files (ADR-0004).
/// One instance per run; algorithms call it from a single thread.
/// </summary>
public sealed class RunObserver(Guid runId, PipelineDetail detail, RunTracker tracker, BlobStore blobs, JsonSerializerOptions json) : IPipelineObserver
{
    private readonly List<StageInfo> _completed = [];
    private readonly List<LayerInfo> _layers = [];
    private ImageF? _dogForWeights;

    /// <summary>Optional prefix for stage ids, used when a run executes the same algorithm on two images.</summary>
    public string StagePrefix { get; set; } = string.Empty;

    public PipelineDetail Detail { get; } = detail;

    public IReadOnlyList<StageInfo> CompletedStages => _completed;

    public IReadOnlyList<LayerInfo> Layers => _layers;

    public static string LayerPath(Guid runId, string layerId) => $"runs/{runId:N}/layers/{layerId}.webp";

    public static string CandidatesPath(Guid runId, string stageId) => $"runs/{runId:N}/candidates/{stageId}.json.br";

    public void StageStarted(string stageId) => tracker.StageStarted(runId, StagePrefix + stageId);

    public void StageCompleted(string stageId, TimeSpan elapsed, StageSummary summary)
    {
        var stage = new StageInfo(StagePrefix + stageId, StageState.Completed, elapsed.TotalMilliseconds, summary.Input, summary.Kept, summary.Rejected);
        _completed.Add(stage);
        tracker.StageCompleted(runId, stage);
    }

    /// <summary>Records a stage that the API itself performs, such as matching or RANSAC.</summary>
    public T Stage<T>(string stageId, Func<T> work, Func<T, (int Input, int Kept, int Rejected)> summarize)
    {
        StageStarted(stageId);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var result = work();
        var (input, kept, rejected) = summarize(result);
        StageCompleted(stageId, System.Diagnostics.Stopwatch.GetElapsedTime(started), new StageSummary(input, kept, rejected));
        return result;
    }

    public void Artifact(string stageId, PipelineArtifact artifact)
    {
        switch (artifact)
        {
            case ImageLayerArtifact layer:
                var gaussian = layer.Kind == ImageLayerKind.Gaussian;
                var rendered = gaussian ? LayerRenderer.Gray(layer.Image) : LayerRenderer.Diverging(layer.Image);
                Save(
                    $"{(gaussian ? "g" : "d")}_{layer.Octave}_{layer.Layer}",
                    gaussian ? "gaussian" : "dog",
                    null,
                    layer.Octave,
                    layer.Layer,
                    layer.Sigma,
                    layer.Image,
                    rendered);
                break;

            case HeatmapArtifact heatmap:
                if (heatmap.Kind == HeatmapKind.DogResponse)
                {
                    _dogForWeights = heatmap.Map;
                }

                var name = JsonNamingPolicy.CamelCase.ConvertName(heatmap.Kind.ToString());
                Save(
                    $"heat_{name}",
                    "heatmap",
                    name,
                    null,
                    null,
                    heatmap.Sigma,
                    heatmap.Map,
                    LayerRenderer.Heatmap(heatmap, heatmap.Kind == HeatmapKind.EdgeRatio ? _dogForWeights : null));
                break;

            case CandidateSetArtifact candidates:
                var dtos = candidates.Candidates.Select(c => new CandidateDto(
                    c.X,
                    c.Y,
                    c.Scale,
                    c.Octave,
                    float.IsNaN(c.Contrast) ? null : c.Contrast,
                    float.IsFinite(c.EdgeRatio) ? c.EdgeRatio : null,
                    c.Rejection == RejectionReason.None ? null : JsonNamingPolicy.CamelCase.ConvertName(c.Rejection.ToString()))).ToList();
                blobs.WriteJson(CandidatesPath(runId, StagePrefix + stageId), dtos, json);
                break;
        }
    }

    private void Save(string id, string kind, string? name, int? octave, int? layer, double sigma, ImageF image, RenderedLayer rendered)
    {
        blobs.Write(LayerPath(runId, id), rendered.WebP);
        _layers.Add(new LayerInfo(id, kind, name, octave, layer, sigma, image.Width, image.Height, rendered.Legend));
    }
}
