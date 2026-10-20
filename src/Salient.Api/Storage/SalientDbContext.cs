using Microsoft.EntityFrameworkCore;
using Salient.Api.Runs;

namespace Salient.Api.Storage;

/// <summary>An uploaded image. The id is derived from its pixels, so re-uploads are deduplicated (ADR-0008).</summary>
public sealed class ImageRecord
{
    public required string Id { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int OriginalWidth { get; set; }

    public int OriginalHeight { get; set; }

    public string? FileName { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>A computation: detection, matching, benchmark or comparison (ADR-0005).</summary>
public sealed class RunRecord
{
    public Guid Id { get; set; }

    public RunKind Kind { get; set; }

    public RunStatus Status { get; set; }

    public required string AlgorithmId { get; set; }

    public required string ImageId { get; set; }

    public string? SecondImageId { get; set; }

    /// <summary>The request payload as JSON; parsed again by the executor.</summary>
    public required string RequestJson { get; set; }

    public required string CacheKey { get; set; }

    public string? Error { get; set; }

    /// <summary>Rendered image layers as JSON (<see cref="LayerInfo"/> list).</summary>
    public string LayersJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public List<RunStageRecord> Stages { get; set; } = [];
}

/// <summary>A completed stage of a run.</summary>
public sealed class RunStageRecord
{
    public int Id { get; set; }

    public Guid RunId { get; set; }

    public int Order { get; set; }

    public required string StageId { get; set; }

    public double DurationMs { get; set; }

    public int Input { get; set; }

    public int Kept { get; set; }

    public int Rejected { get; set; }
}

/// <summary>EF Core context over SQLite.</summary>
public sealed class SalientDbContext(DbContextOptions<SalientDbContext> options) : DbContext(options)
{
    public DbSet<ImageRecord> Images => Set<ImageRecord>();

    public DbSet<RunRecord> Runs => Set<RunRecord>();

    public DbSet<RunStageRecord> RunStages => Set<RunStageRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImageRecord>().HasKey(i => i.Id);
        modelBuilder.Entity<RunRecord>(run =>
        {
            run.HasKey(r => r.Id);
            run.HasIndex(r => r.CacheKey);
            run.HasIndex(r => r.CreatedAt);
            run.Property(r => r.Kind).HasConversion<string>();
            run.Property(r => r.Status).HasConversion<string>();
            run.HasMany(r => r.Stages).WithOne().HasForeignKey(s => s.RunId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
