using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Salient.Api.Algorithms;
using Salient.Api.Images;
using Salient.Api.Storage;
using Salient.Evaluation;
using Salient.Reference.OpenCv;

namespace Salient.Api.Runs;

/// <summary>Thrown for requests that refer to missing images or algorithms, or that cannot be queued.</summary>
public sealed class RunRequestException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>The bounded in-process job queue (ADR-0005).</summary>
public sealed class RunDispatcher(IOptions<SalientOptions> options)
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(options.Value.QueueCapacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
    });

    public ChannelReader<Guid> Reader => _channel.Reader;

    public bool TryEnqueue(Guid runId) => _channel.Writer.TryWrite(runId);
}

/// <summary>Creates runs (with result caching), reports their state and cancels them.</summary>
public sealed class RunService(
    IDbContextFactory<SalientDbContext> dbFactory,
    RunTracker tracker,
    RunDispatcher queue,
    AlgorithmRegistry registry,
    ImageService images)
{
    public async Task<RunCreated> CreateDetectionAsync(DetectionRequest request, CancellationToken cancellationToken)
    {
        await RequireImageAsync(request.ImageId, cancellationToken);
        var algorithm = RequireAlgorithm(request.AlgorithmId);
        var parameters = algorithm.Parse(request.Parameters);
        var key = $"detection|{request.ImageId}|{algorithm.Descriptor.Id}|{algorithm.Descriptor.Version}|{algorithm.Canonical(parameters)}";
        return await CreateAsync(RunKind.Detection, algorithm.Descriptor.Id, request.ImageId, null, request, key, cancellationToken);
    }

    public async Task<RunCreated> CreateMatchingAsync(MatchingRequest request, CancellationToken cancellationToken)
    {
        await RequireImageAsync(request.ImageA, cancellationToken);
        await RequireImageAsync(request.ImageB, cancellationToken);
        var algorithm = RequireAlgorithm(request.AlgorithmId);
        if (!algorithm.Describes)
        {
            throw new ValidationException($"Algorithm '{algorithm.Descriptor.Id}' does not compute descriptors.");
        }

        if (request.Ratio is <= 0 or > 1 || request.RansacThreshold is <= 0 or > 50)
        {
            throw new ValidationException("Ratio must be in (0, 1] and the RANSAC threshold in (0, 50] pixels.");
        }

        var parameters = algorithm.Parse(request.Parameters);
        var key = $"matching|{request.ImageA}|{request.ImageB}|{algorithm.Descriptor.Id}|{algorithm.Descriptor.Version}|{algorithm.Canonical(parameters)}|{request.Ratio}|{request.RansacThreshold}";
        return await CreateAsync(RunKind.Matching, algorithm.Descriptor.Id, request.ImageA, request.ImageB, request, key, cancellationToken);
    }

    public async Task<RunCreated> CreateBenchmarkAsync(BenchmarkRequest request, CancellationToken cancellationToken)
    {
        await RequireImageAsync(request.ImageId, cancellationToken);
        if (request.AlgorithmIds.Count == 0)
        {
            throw new ValidationException("Choose at least one algorithm.");
        }

        var algorithms = request.AlgorithmIds.Select(RequireAlgorithm).ToList();
        if (algorithms.FirstOrDefault(a => !a.Describes) is { } nonDescribing)
        {
            throw new ValidationException($"Algorithm '{nonDescribing.Descriptor.Id}' does not compute descriptors.");
        }

        var families = request.Families ?? Transformations.DefaultValues.ToDictionary(p => p.Key, p => p.Value);
        if (families.Count == 0 || families.Values.Any(v => v.Length is 0 or > 32))
        {
            throw new ValidationException("Each family needs between 1 and 32 values.");
        }

        var normalized = request with { Families = families };
        var versions = string.Join(',', algorithms.Select(a => $"{a.Descriptor.Id}@{a.Descriptor.Version}"));
        var key = $"benchmark|{request.ImageId}|{versions}|{JsonSerializer.Serialize(families, ApiJson.Options)}";
        return await CreateAsync(RunKind.Benchmark, string.Join(',', algorithms.Select(a => a.Descriptor.Id)), request.ImageId, null, normalized, key, cancellationToken);
    }

    public async Task<RunCreated> CreateComparisonAsync(ComparisonRequest request, CancellationToken cancellationToken)
    {
        await RequireImageAsync(request.ImageId, cancellationToken);
        var sift = RequireAlgorithm("sift");
        if (registry.Find("opencv-sift") is null)
        {
            throw new RunRequestException(StatusCodes.Status503ServiceUnavailable, "The OpenCV reference is not available on this server.");
        }

        var parameters = sift.Parse(request.Parameters);
        var key = $"comparison|{request.ImageId}|{sift.Descriptor.Version}|{OpenCvAvailability.Version}|{sift.Canonical(parameters)}";
        return await CreateAsync(RunKind.Comparison, "sift", request.ImageId, null, request, key, cancellationToken);
    }

    public async Task<RunInfo?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.Runs.AsNoTracking().Include(r => r.Stages).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var live = tracker.Find(id);
        var stages = live is not null && record.Status is RunStatus.Queued or RunStatus.Running
            ? live.Stages
            : record.Stages.OrderBy(s => s.Order).Select(s => new StageInfo(s.StageId, StageState.Completed, s.DurationMs, s.Input, s.Kept, s.Rejected)).ToList();
        var duration = record is { StartedAt: { } start, CompletedAt: { } end } ? (end - start).TotalMilliseconds : (double?)null;

        return new RunInfo(
            record.Id,
            record.Kind,
            live?.Status is RunStatus.Running ? RunStatus.Running : record.Status,
            record.AlgorithmId,
            record.ImageId,
            record.SecondImageId,
            DateTime.SpecifyKind(record.CreatedAt, DateTimeKind.Utc),
            duration,
            record.Error,
            stages,
            JsonSerializer.Deserialize<List<LayerInfo>>(record.LayersJson, ApiJson.Options) ?? [],
            live?.Progress);
    }

    /// <summary>Cancels a queued or running run. Returns false when it is unknown or already finished.</summary>
    public bool Cancel(Guid id) => tracker.Cancel(id);

    private async Task<RunCreated> CreateAsync<TRequest>(
        RunKind kind,
        string algorithmId,
        string imageId,
        string? secondImageId,
        TRequest request,
        string cacheSource,
        CancellationToken cancellationToken)
    {
        var cacheKey = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(cacheSource)));
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var cached = await db.Runs.AsNoTracking()
            .Where(r => r.CacheKey == cacheKey && r.Status == RunStatus.Completed)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (cached is { } cachedId)
        {
            return new RunCreated(cachedId, RunStatus.Completed, Cached: true);
        }

        var record = new RunRecord
        {
            Id = Guid.CreateVersion7(),
            Kind = kind,
            Status = RunStatus.Queued,
            AlgorithmId = algorithmId,
            ImageId = imageId,
            SecondImageId = secondImageId,
            RequestJson = JsonSerializer.Serialize(request, ApiJson.Options),
            CacheKey = cacheKey,
            CreatedAt = DateTime.UtcNow,
        };
        db.Runs.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        tracker.Register(record.Id);
        if (!queue.TryEnqueue(record.Id))
        {
            tracker.Remove(record.Id);
            record.Status = RunStatus.Failed;
            record.Error = "The server is busy.";
            await db.SaveChangesAsync(cancellationToken);
            throw new RunRequestException(StatusCodes.Status503ServiceUnavailable, "The server is busy; try again shortly.");
        }

        return new RunCreated(record.Id, RunStatus.Queued, Cached: false);
    }

    private async Task RequireImageAsync(string id, CancellationToken cancellationToken)
    {
        if (await images.FindAsync(id, cancellationToken) is null)
        {
            throw new RunRequestException(StatusCodes.Status404NotFound, $"Image '{id}' was not found.");
        }
    }

    private AlgorithmRegistration RequireAlgorithm(string id) =>
        registry.Find(id) ?? throw new RunRequestException(StatusCodes.Status404NotFound, $"Algorithm '{id}' is not available.");
}
