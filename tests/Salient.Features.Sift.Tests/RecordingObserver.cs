using Salient.Features.Pipeline;

namespace Salient.Features.Sift.Tests;

internal sealed class RecordingObserver(PipelineDetail detail) : IPipelineObserver
{
    public PipelineDetail Detail { get; } = detail;

    public List<string> Started { get; } = [];

    public List<string> Completed { get; } = [];

    public Dictionary<string, StageSummary> Summaries { get; } = [];

    public List<PipelineArtifact> Artifacts { get; } = [];

    private List<(string Stage, PipelineArtifact Artifact)> StagedArtifacts { get; } = [];

    public void StageStarted(string stageId) => Started.Add(stageId);

    public void StageCompleted(string stageId, TimeSpan elapsed, StageSummary summary)
    {
        Completed.Add(stageId);
        Summaries[stageId] = summary;
    }

    public void Artifact(string stageId, PipelineArtifact artifact)
    {
        Artifacts.Add(artifact);
        StagedArtifacts.Add((stageId, artifact));
    }

    public IEnumerable<T> ArtifactsOf<T>(string stageId) where T : PipelineArtifact =>
        StagedArtifacts.Where(a => a.Stage == stageId).Select(a => a.Artifact).OfType<T>();
}
