using Microsoft.AspNetCore.SignalR;

namespace Salient.Api.Runs;

/// <summary>Events pushed to clients subscribed to a run (ADR-0005).</summary>
public interface IRunClient
{
    Task StageStarted(Guid runId, string stageId);

    Task StageCompleted(Guid runId, StageInfo stage);

    Task Progress(Guid runId, RunProgress progress);

    Task RunCompleted(Guid runId);

    Task RunFailed(Guid runId, string message);

    Task RunCanceled(Guid runId);
}

/// <summary>Live run progress at <c>/hubs/runs</c>.</summary>
public sealed class RunsHub(RunService runs) : Hub<IRunClient>
{
    public static string Group(Guid runId) => $"run:{runId:N}";

    /// <summary>
    /// Subscribes to a run and returns its current state, so a client that subscribes after the run started (or finished)
    /// misses nothing.
    /// </summary>
    public async Task<RunInfo?> Subscribe(Guid runId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(runId));
        return await runs.GetAsync(runId, Context.ConnectionAborted);
    }

    public Task Unsubscribe(Guid runId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(runId));
}
