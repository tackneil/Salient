using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Salient.Api.Runs;

/// <summary>In-memory state of queued and running runs, and the bridge to the SignalR hub.</summary>
public sealed class RunTracker(IHubContext<RunsHub, IRunClient> hub, ILogger<RunTracker> logger)
{
    private readonly ConcurrentDictionary<Guid, LiveRun> _live = new();

    /// <summary>A run that has not finished yet.</summary>
    public sealed class LiveRun(Guid id)
    {
        private readonly List<StageInfo> _stages = [];

        public Guid Id { get; } = id;

        public CancellationTokenSource Cancellation { get; } = new();

        public RunStatus Status { get; set; } = RunStatus.Queued;

        public RunProgress? Progress { get; set; }

        public IReadOnlyList<StageInfo> Stages
        {
            get
            {
                lock (_stages)
                {
                    return [.. _stages];
                }
            }
        }

        internal void Upsert(StageInfo stage)
        {
            lock (_stages)
            {
                var index = _stages.FindIndex(s => s.Id == stage.Id);
                if (index >= 0)
                {
                    _stages[index] = stage;
                }
                else
                {
                    _stages.Add(stage);
                }
            }
        }
    }

    public LiveRun Register(Guid id) => _live.GetOrAdd(id, i => new LiveRun(i));

    public LiveRun? Find(Guid id) => _live.GetValueOrDefault(id);

    public void Remove(Guid id)
    {
        if (_live.TryRemove(id, out var run))
        {
            run.Cancellation.Dispose();
        }
    }

    /// <summary>Requests cancellation; returns false when the run is not live.</summary>
    public bool Cancel(Guid id)
    {
        if (!_live.TryGetValue(id, out var run))
        {
            return false;
        }

        run.Cancellation.Cancel();
        return true;
    }

    public void StageStarted(Guid id, string stageId)
    {
        Find(id)?.Upsert(new StageInfo(stageId, StageState.Running, null, 0, 0, 0));
        Send(id, c => c.StageStarted(id, stageId));
    }

    public void StageCompleted(Guid id, StageInfo stage)
    {
        Find(id)?.Upsert(stage);
        Send(id, c => c.StageCompleted(id, stage));
    }

    public void Progress(Guid id, RunProgress progress)
    {
        if (Find(id) is { } run)
        {
            run.Progress = progress;
        }

        Send(id, c => c.Progress(id, progress));
    }

    public void Completed(Guid id) => Send(id, c => c.RunCompleted(id));

    public void Failed(Guid id, string message) => Send(id, c => c.RunFailed(id, message));

    public void Canceled(Guid id) => Send(id, c => c.RunCanceled(id));

    /// <summary>Fire-and-forget: the algorithm thread must not wait for network I/O.</summary>
    private void Send(Guid id, Func<IRunClient, Task> send)
    {
        send(hub.Clients.Group(RunsHub.Group(id))).ContinueWith(
            t => Log.NotifyFailed(logger, t.Exception, id),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
