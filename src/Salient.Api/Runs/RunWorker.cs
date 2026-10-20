using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Salient.Api.Storage;

namespace Salient.Api.Runs;

/// <summary>Executes queued runs with a fixed number of parallel workers (ADR-0005).</summary>
public sealed class RunWorker(
    RunDispatcher queue,
    RunTracker tracker,
    RunExecutor executor,
    IDbContextFactory<SalientDbContext> dbFactory,
    IOptions<SalientOptions> options,
    ILogger<RunWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MarkInterruptedRunsAsync(stoppingToken);
        var workers = Enumerable.Range(0, options.Value.WorkerCount).Select(_ => WorkAsync(stoppingToken));
        await Task.WhenAll(workers);
    }

    private async Task WorkAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var runId in queue.Reader.ReadAllAsync(stoppingToken))
            {
                await ExecuteAsync(runId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is stopping.
        }
    }

    private async Task ExecuteAsync(Guid runId, CancellationToken stoppingToken)
    {
        var live = tracker.Register(runId);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(live.Cancellation.Token, stoppingToken);
        var token = linked.Token;
        await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);
        var record = await db.Runs.FirstAsync(r => r.Id == runId, stoppingToken);

        try
        {
            token.ThrowIfCancellationRequested();
            record.Status = live.Status = RunStatus.Running;
            record.StartedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(stoppingToken);

            var observer = await Task.Run(() => executor.ExecuteAsync(record, token), token);

            record.Stages = observer.CompletedStages.Select((s, i) => new RunStageRecord
            {
                RunId = runId,
                Order = i,
                StageId = s.Id,
                DurationMs = s.DurationMs ?? 0,
                Input = s.Input,
                Kept = s.Kept,
                Rejected = s.Rejected,
            }).ToList();
            record.LayersJson = JsonSerializer.Serialize(observer.Layers, ApiJson.Options);
            record.Status = RunStatus.Completed;
            record.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(stoppingToken);
            tracker.Completed(runId);
        }
        catch (OperationCanceledException) when (live.Cancellation.IsCancellationRequested)
        {
            await FinishAsync(db, record, RunStatus.Canceled, "Canceled.", stoppingToken);
            tracker.Canceled(runId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            Log.RunFailed(logger, ex, runId);
            var message = ex is RunRequestException or System.ComponentModel.DataAnnotations.ValidationException ? ex.Message : "The run failed unexpectedly.";
            await FinishAsync(db, record, RunStatus.Failed, message, stoppingToken);
            tracker.Failed(runId, message);
        }
        finally
        {
            tracker.Remove(runId);
        }
    }

    private static async Task FinishAsync(SalientDbContext db, RunRecord record, RunStatus status, string message, CancellationToken token)
    {
        record.Status = status;
        record.Error = message;
        record.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(token);
    }

    /// <summary>The queue lives in memory, so runs left queued or running by a previous process can never finish (ADR-0005).</summary>
    private async Task MarkInterruptedRunsAsync(CancellationToken token)
    {
        await using var db = await dbFactory.CreateDbContextAsync(token);
        await db.Runs
            .Where(r => r.Status == RunStatus.Queued || r.Status == RunStatus.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RunStatus.Failed).SetProperty(r => r.Error, "Interrupted by a server restart."), token);
    }
}
