using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Salient.Api.Runs;

namespace Salient.Api.Storage;

/// <summary>Deletes finished runs and unused images older than the retention period (ADR-0008).</summary>
public sealed class RetentionService(
    IDbContextFactory<SalientDbContext> dbFactory,
    BlobStore blobs,
    IOptions<SalientOptions> options,
    ILogger<RetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await CleanAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.RetentionFailed(logger, ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task CleanAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-options.Value.RetentionDays);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var oldRuns = await db.Runs
            .Where(r => r.CreatedAt < cutoff && r.Status != RunStatus.Queued && r.Status != RunStatus.Running)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in oldRuns)
        {
            blobs.DeleteFolder($"runs/{id:N}");
        }

        await db.Runs.Where(r => oldRuns.Contains(r.Id)).ExecuteDeleteAsync(cancellationToken);

        var oldImages = await db.Images
            .Where(i => i.CreatedAt < cutoff && !db.Runs.Any(r => r.ImageId == i.Id || r.SecondImageId == i.Id))
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in oldImages)
        {
            blobs.DeleteFolder($"images/{id}");
        }

        await db.Images.Where(i => oldImages.Contains(i.Id)).ExecuteDeleteAsync(cancellationToken);
        if (oldRuns.Count + oldImages.Count > 0)
        {
            Log.RetentionRemoved(logger, oldRuns.Count, oldImages.Count);
        }
    }
}
