using System.Diagnostics;

namespace Salient.Features.Pipeline;

/// <summary>Null-safe helper for reporting stages to an optional <see cref="IPipelineObserver"/>.</summary>
public readonly struct PipelineReporter(IPipelineObserver? observer)
{
    private readonly IPipelineObserver? _observer = observer is { Detail: not PipelineDetail.None } ? observer : null;

    /// <summary><see langword="true"/> when intermediate artifacts should be produced.</summary>
    public bool WantsArtifacts => _observer?.Detail == PipelineDetail.Full;

    /// <summary>Runs one stage, reporting its start, duration and summary.</summary>
    /// <typeparam name="T">Stage result.</typeparam>
    /// <param name="stageId">Stable stage identifier.</param>
    /// <param name="work">The stage.</param>
    /// <param name="summarize">Builds the stage summary from the result.</param>
    public T Run<T>(string stageId, Func<T> work, Func<T, StageSummary> summarize)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(summarize);
        if (_observer is null)
        {
            return work();
        }

        _observer.StageStarted(stageId);
        var started = Stopwatch.GetTimestamp();
        var result = work();
        _observer.StageCompleted(stageId, Stopwatch.GetElapsedTime(started), summarize(result));
        return result;
    }

    /// <summary>Reports an artifact when <see cref="WantsArtifacts"/> is set; the factory is not called otherwise.</summary>
    public void Artifact(string stageId, Func<PipelineArtifact> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        if (WantsArtifacts)
        {
            _observer!.Artifact(stageId, create());
        }
    }
}
