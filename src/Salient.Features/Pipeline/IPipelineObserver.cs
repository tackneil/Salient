namespace Salient.Features.Pipeline;

/// <summary>How much an algorithm reports while it runs (ADR-0004).</summary>
public enum PipelineDetail
{
    /// <summary>No reporting; fastest. Used by benchmarks and tests.</summary>
    None,

    /// <summary>Stage start and completion with timings and counts.</summary>
    Progress,

    /// <summary>Progress plus intermediate artifacts (image layers and candidate sets).</summary>
    Full,
}

/// <summary>Receives stage progress and intermediate results from an algorithm.</summary>
/// <remarks>Calls arrive on the thread running the algorithm, one stage at a time.</remarks>
public interface IPipelineObserver
{
    /// <summary>The level of detail the observer wants.</summary>
    PipelineDetail Detail { get; }

    /// <summary>A stage has started.</summary>
    /// <param name="stageId">Stable stage identifier declared by the algorithm.</param>
    void StageStarted(string stageId);

    /// <summary>A stage has finished.</summary>
    /// <param name="stageId">Stable stage identifier declared by the algorithm.</param>
    /// <param name="elapsed">Wall-clock time spent in the stage.</param>
    /// <param name="summary">Candidate counts for the stage.</param>
    void StageCompleted(string stageId, TimeSpan elapsed, StageSummary summary);

    /// <summary>An intermediate result. Only called when <see cref="Detail"/> is <see cref="PipelineDetail.Full"/>.</summary>
    /// <param name="stageId">Stage that produced the artifact.</param>
    /// <param name="artifact">The artifact; image data must be consumed or copied before the call returns if it is kept.</param>
    void Artifact(string stageId, PipelineArtifact artifact);
}

/// <summary>Candidate counts for one stage.</summary>
/// <param name="Input">Candidates entering the stage.</param>
/// <param name="Kept">Candidates leaving the stage. May exceed <paramref name="Input"/> when a stage splits points, for example one keypoint per orientation peak.</param>
/// <param name="Rejected">Candidates the stage discarded.</param>
public sealed record StageSummary(int Input, int Kept, int Rejected)
{
    /// <summary>Summary for stages that do not work with candidates, such as building a pyramid.</summary>
    public static StageSummary None { get; } = new(0, 0, 0);
}
