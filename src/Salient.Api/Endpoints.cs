using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Salient.Api.Algorithms;
using Salient.Api.Images;
using Salient.Api.Runs;
using Salient.Api.Storage;
using Salient.Features;

namespace Salient.Api;

/// <summary>Minimal API endpoints, grouped by feature (ADR-0010).</summary>
public static class Endpoints
{
    public const string JobsRateLimit = "jobs";

    public static void MapSalientApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/algorithms", (AlgorithmRegistry registry) => TypedResults.Ok(registry.All.Select(r => r.Info).ToList()))
            .WithName("ListAlgorithms").WithTags("Algorithms");

        var images = api.MapGroup("/images").WithTags("Images");
        images.MapPost("/", UploadImageAsync).WithName("UploadImage").DisableAntiforgery().RequireRateLimiting(JobsRateLimit);
        images.MapGet("/{id}", GetImageAsync).WithName("GetImage");
        images.MapGet("/{id}/content", GetImageContentAsync).WithName("GetImageContent");

        var runs = api.MapGroup("/runs").WithTags("Runs");
        runs.MapPost("/", (DetectionRequest r, RunService s, CancellationToken ct) => CreateAsync(() => s.CreateDetectionAsync(r, ct)))
            .WithName("CreateDetectionRun").RequireRateLimiting(JobsRateLimit);
        runs.MapGet("/{id:guid}", GetRunAsync).WithName("GetRun");
        runs.MapDelete("/{id:guid}", CancelRun).WithName("CancelRun");
        runs.MapGet("/{id:guid}/detection", (Guid id, BlobStore b, CancellationToken ct) => ResultAsync<DetectionResultDto>(b, RunExecutor.DetectionPath(id), ct))
            .WithName("GetDetectionResult");
        runs.MapGet("/{id:guid}/keypoints/{index:int}", GetKeypointAsync).WithName("GetKeypoint");
        runs.MapGet("/{id:guid}/candidates/{stageId}", (Guid id, string stageId, BlobStore b, CancellationToken ct) =>
            ResultAsync<List<CandidateDto>>(b, RunObserver.CandidatesPath(id, stageId), ct)).WithName("GetStageCandidates");
        runs.MapGet("/{id:guid}/layers/{layerId}", GetLayer).WithName("GetLayer");
        runs.MapGet("/{id:guid}/matching", (Guid id, BlobStore b, CancellationToken ct) => ResultAsync<MatchingResultDto>(b, RunExecutor.MatchingPath(id), ct))
            .WithName("GetMatchingResult");
        runs.MapGet("/{id:guid}/benchmark", (Guid id, BlobStore b, CancellationToken ct) => ResultAsync<BenchmarkResultDto>(b, RunExecutor.BenchmarkPath(id), ct))
            .WithName("GetBenchmarkResult");
        runs.MapGet("/{id:guid}/comparison", (Guid id, BlobStore b, CancellationToken ct) => ResultAsync<ComparisonResultDto>(b, RunExecutor.ComparisonPath(id), ct))
            .WithName("GetComparisonResult");

        api.MapPost("/matches", (MatchingRequest r, RunService s, CancellationToken ct) => CreateAsync(() => s.CreateMatchingAsync(r, ct)))
            .WithName("CreateMatchingRun").WithTags("Runs").RequireRateLimiting(JobsRateLimit);
        api.MapPost("/benchmarks", (BenchmarkRequest r, RunService s, CancellationToken ct) => CreateAsync(() => s.CreateBenchmarkAsync(r, ct)))
            .WithName("CreateBenchmarkRun").WithTags("Runs").RequireRateLimiting(JobsRateLimit);
        api.MapPost("/comparisons", (ComparisonRequest r, RunService s, CancellationToken ct) => CreateAsync(() => s.CreateComparisonAsync(r, ct)))
            .WithName("CreateComparisonRun").WithTags("Runs").RequireRateLimiting(JobsRateLimit);

        // Unknown API routes must not fall through to the SPA.
        api.MapFallback(() => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found"));
    }

    private static async Task<Results<Created<ImageInfo>, ProblemHttpResult>> UploadImageAsync(
        IFormFile file,
        ImageService images,
        IOptions<SalientOptions> options,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0 || file.Length > options.Value.MaxUploadBytes)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid upload",
                detail: $"The file must be between 1 byte and {options.Value.MaxUploadBytes / (1024 * 1024)} MB.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var info = await images.StoreAsync(stream, file.FileName, cancellationToken);
            return TypedResults.Created($"/api/images/{info.Id}", info);
        }
        catch (InvalidImageException ex)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid image", detail: ex.Message);
        }
    }

    private static async Task<Results<Ok<ImageInfo>, NotFound>> GetImageAsync(string id, ImageService images, CancellationToken cancellationToken) =>
        await images.FindAsync(id, cancellationToken) is { } info ? TypedResults.Ok(info) : TypedResults.NotFound();

    private static async Task<IResult> GetImageContentAsync(string id, ImageService images, BlobStore blobs, HttpContext context, CancellationToken cancellationToken)
    {
        if (await images.FindAsync(id, cancellationToken) is null)
        {
            return TypedResults.NotFound();
        }

        Immutable(context);
        return TypedResults.Stream(blobs.OpenRead(ImageService.DisplayPath(id)), "image/webp");
    }

    private static async Task<Results<Accepted<RunCreated>, Ok<RunCreated>, ValidationProblem, ProblemHttpResult>> CreateAsync(Func<Task<RunCreated>> create)
    {
        try
        {
            var created = await create();
            return created.Cached
                ? TypedResults.Ok(created)
                : TypedResults.Accepted($"/api/runs/{created.RunId}", created);
        }
        catch (ValidationException ex)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["parameters"] = [ex.Message] });
        }
        catch (RunRequestException ex)
        {
            return TypedResults.Problem(statusCode: ex.StatusCode, detail: ex.Message);
        }
    }

    private static async Task<Results<Ok<RunInfo>, NotFound>> GetRunAsync(Guid id, RunService runs, CancellationToken cancellationToken) =>
        await runs.GetAsync(id, cancellationToken) is { } info ? TypedResults.Ok(info) : TypedResults.NotFound();

    private static Results<Accepted, Conflict<ProblemDetails>> CancelRun(Guid id, RunService runs) =>
        runs.Cancel(id)
            ? TypedResults.Accepted((string?)null)
            : TypedResults.Conflict(new ProblemDetails { Title = "Not running", Detail = "The run is unknown or has already finished." });

    private static async Task<Results<Ok<KeypointDetail>, NotFound>> GetKeypointAsync(Guid id, int index, BlobStore blobs, CancellationToken cancellationToken)
    {
        if (!blobs.Exists(RunExecutor.DetectionPath(id)))
        {
            return TypedResults.NotFound();
        }

        var result = await blobs.ReadJsonAsync<DetectionResultDto>(RunExecutor.DetectionPath(id), ApiJson.Options, cancellationToken);
        if (result is null || index < 0 || index >= result.Keypoints.Count)
        {
            return TypedResults.NotFound();
        }

        float[]? histogram = null;
        if (blobs.Exists(RunExecutor.HistogramsPath(id)))
        {
            var bins = (int)(new FileInfo(Path.Combine(blobs.Root, RunExecutor.HistogramsPath(id))).Length / sizeof(float) / result.Keypoints.Count);
            histogram = MemoryMarshal.Cast<byte, float>(await ReadSliceAsync(blobs, RunExecutor.HistogramsPath(id), (long)index * bins * sizeof(float), bins * sizeof(float), cancellationToken)).ToArray();
        }

        float[]? real = null;
        byte[]? binary = null;
        if (result.Descriptor is { } descriptor && blobs.Exists(RunExecutor.DescriptorsPath(id)))
        {
            var size = descriptor.Kind == DescriptorKind.Real ? descriptor.Dimension * sizeof(float) : descriptor.Dimension;
            var bytes = await ReadSliceAsync(blobs, RunExecutor.DescriptorsPath(id), (long)index * size, size, cancellationToken);
            if (descriptor.Kind == DescriptorKind.Real)
            {
                real = MemoryMarshal.Cast<byte, float>(bytes).ToArray();
            }
            else
            {
                binary = bytes;
            }
        }

        return TypedResults.Ok(new KeypointDetail(index, result.Keypoints[index], histogram, real, binary));
    }

    private static IResult GetLayer(Guid id, string layerId, BlobStore blobs, HttpContext context)
    {
        if (layerId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-'))
        {
            return TypedResults.BadRequest();
        }

        var path = RunObserver.LayerPath(id, layerId);
        if (!blobs.Exists(path))
        {
            return TypedResults.NotFound();
        }

        Immutable(context);
        return TypedResults.Stream(blobs.OpenRead(path), "image/webp");
    }

    private static async Task<Results<Ok<T>, NotFound>> ResultAsync<T>(BlobStore blobs, string path, CancellationToken cancellationToken)
    {
        if (!blobs.Exists(path))
        {
            return TypedResults.NotFound();
        }

        var value = await blobs.ReadJsonAsync<T>(path, ApiJson.Options, cancellationToken);
        return value is null ? TypedResults.NotFound() : TypedResults.Ok(value);
    }

    private static async Task<byte[]> ReadSliceAsync(BlobStore blobs, string path, long offset, int count, CancellationToken cancellationToken)
    {
        await using var stream = blobs.OpenRead(path);
        stream.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[count];
        await stream.ReadExactlyAsync(buffer, cancellationToken);
        return buffer;
    }

    /// <summary>Run layers and image content never change for a given id (ADR-0006).</summary>
    private static void Immutable(HttpContext context) =>
        context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
}
