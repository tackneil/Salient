using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Salient.Api.Algorithms;
using Salient.Api.Runs;
using Salient.Evaluation;
using Salient.Features.Sift.Tests;
using Salient.Reference.OpenCv;

namespace Salient.Api.Tests;

public sealed class ApiTests(SalientApiFactory factory) : IClassFixture<SalientApiFactory>
{
    private static readonly Core.ImageF Scene = GoldenScene.Create(320, 240, seed: 2026);

    [Fact]
    public async Task Algorithms_expose_parameter_schemas()
    {
        using var client = factory.CreateClient();

        var algorithms = await client.GetJsonAsync<List<AlgorithmInfo>>("/api/algorithms");

        var sift = Assert.Single(algorithms, a => a.Id == "sift");
        Assert.Contains("Describes", sift.Capabilities);
        var contrast = sift.ParameterSchema["properties"]!["contrastThreshold"]!;
        Assert.Equal("number", (string?)contrast["type"]);
        Assert.Equal(0.005, (double)contrast["minimum"]!);
        Assert.Equal(0.04, (double)contrast["default"]!);
        Assert.Equal(0.0025, (double)contrast["x-step"]!);
        Assert.True((bool)sift.ParameterSchema["properties"]!["upsampleInput"]!["x-advanced"]!);
        Assert.Equal(OpenCvAvailability.IsAvailable, algorithms.Any(a => a.Id == "opencv-orb"));
    }

    [Fact]
    public async Task Uploads_are_deduplicated_and_invalid_files_rejected()
    {
        using var client = factory.CreateClient();

        var first = await client.UploadAsync(Scene, "a.png");
        var second = await client.UploadAsync(Scene, "b.png");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal((320, 240), (first.Width, first.Height));
        using var content = new MultipartFormDataContent { { new ByteArrayContent("not an image"u8.ToArray()), "file", "x.png" } };
        using var response = await client.PostAsync(new Uri("/api/images", UriKind.Relative), content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Detection_run_serves_keypoints_layers_candidates_and_inspector_data()
    {
        using var client = factory.CreateClient();
        var image = await client.UploadAsync(Scene);
        var request = new DetectionRequest(image.Id, "sift", JsonSerializer.SerializeToElement(new { contrastThreshold = 0.04 }));

        var created = await client.PostRunAsync("/api/runs", request);
        var run = await client.WaitAsync(created.RunId);

        Assert.Equal(RunStatus.Completed, run.Status);
        string[] stages = ["load", "gaussian-pyramid", "dog", "extrema", "refine", "contrast", "edge", "orientation", "heatmaps", "descriptor"];
        Assert.Equal(stages, run.Stages.Select(s => s.Id));
        Assert.Contains(run.Layers, l => l.Id == "g_0_0" && l.Legend.Colormap == "gray");
        Assert.Contains(run.Layers, l => l.Id == "d_0_1" && l.Legend.Colormap == "diverging");
        Assert.Contains(run.Layers, l => l.Kind == "heatmap" && l.Name == "edgeRatio" && l.Legend.Scale == "log");

        var detection = await client.GetJsonAsync<DetectionResultDto>($"/api/runs/{run.Id}/detection");
        Assert.True(detection.Keypoints.Count > 50);
        Assert.Equal(0.005, detection.ExplorationParameters.GetProperty("contrastThreshold").GetDouble());
        Assert.Equal(0.04, detection.Parameters.GetProperty("contrastThreshold").GetDouble());
        Assert.Contains(detection.Keypoints, k => k.Contrast < 0.04f);
        Assert.Equal(128, detection.Descriptor!.Dimension);

        var detail = await client.GetJsonAsync<KeypointDetail>($"/api/runs/{run.Id}/keypoints/3");
        Assert.Equal(36, detail.OrientationHistogram!.Count);
        Assert.Equal(128, detail.Descriptor!.Count);
        Assert.Equal(detection.Keypoints[3], detail.Keypoint);

        var edgeCandidates = await client.GetJsonAsync<List<CandidateDto>>($"/api/runs/{run.Id}/candidates/edge");
        Assert.Contains(edgeCandidates, c => c.Rejection == "edgeResponse");

        using var layer = await client.GetAsync(new Uri($"/api/runs/{run.Id}/layers/heat_gradientMagnitude", UriKind.Relative));
        Assert.Equal("image/webp", layer.Content.Headers.ContentType!.MediaType);
        Assert.Contains("immutable", layer.Headers.CacheControl!.ToString());

        var again = await client.PostRunAsync("/api/runs", request);
        Assert.True(again.Cached);
        Assert.Equal(run.Id, again.RunId);
    }

    [Fact]
    public async Task Invalid_requests_return_problem_details()
    {
        using var client = factory.CreateClient();
        var image = await client.UploadAsync(Scene);

        using var badParameters = await client.PostAsJsonAsync(
            new Uri("/api/runs", UriKind.Relative),
            new DetectionRequest(image.Id, "sift", JsonSerializer.SerializeToElement(new { contrastThreshold = 5 })),
            SalientApiFactory.Json);
        using var unknownAlgorithm = await client.PostAsJsonAsync(new Uri("/api/runs", UriKind.Relative), new DetectionRequest(image.Id, "nope", null), SalientApiFactory.Json);
        using var unknownImage = await client.PostAsJsonAsync(new Uri("/api/runs", UriKind.Relative), new DetectionRequest("missing", "sift", null), SalientApiFactory.Json);
        using var unknownRoute = await client.GetAsync(new Uri("/api/nothing-here", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, badParameters.StatusCode);
        Assert.Equal("application/problem+json", badParameters.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, unknownAlgorithm.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownImage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownRoute.StatusCode);
    }

    [Fact]
    public async Task Matching_run_recovers_rotation_and_scale()
    {
        using var client = factory.CreateClient();
        var (warped, _) = GoldenScene.RotateAndScale(Scene, degrees: 10, scale: 0.85);
        var a = await client.UploadAsync(Scene);
        var b = await client.UploadAsync(warped, "rotated.png");

        var created = await client.PostRunAsync("/api/matches", new MatchingRequest(a.Id, b.Id, "sift", null));
        var run = await client.WaitAsync(created.RunId);
        var result = await client.GetJsonAsync<MatchingResultDto>($"/api/runs/{run.Id}/matching");

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.InRange(result.RotationDegrees!.Value, 9.5, 10.5);
        Assert.InRange(result.Scale!.Value, 0.84, 0.86);
        Assert.True(result.InlierCount > 30);
        Assert.Contains(result.Matches, m => m.Ratio > 0.8 && !m.Inlier);
        Assert.Contains(run.Stages, s => s.Id == "a:orientation");
    }

    [Fact]
    public async Task Benchmark_progress_is_pushed_over_SignalR()
    {
        using var client = factory.CreateClient();
        var image = await client.UploadAsync(Scene);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new List<RunProgress>();
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/runs"), o =>
            {
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(o => ApiJson.Configure(o.PayloadSerializerOptions))
            .Build();
        connection.On<Guid, RunProgress>(nameof(IRunClient.Progress), (_, p) => { lock (progress) { progress.Add(p); } });
        connection.On<Guid>(nameof(IRunClient.RunCompleted), _ => completed.TrySetResult());
        await connection.StartAsync();

        var families = new Dictionary<TransformationFamily, double[]> { [TransformationFamily.Rotation] = [0, 20, 40] };
        var created = await client.PostRunAsync("/api/benchmarks", new BenchmarkRequest(image.Id, ["sift"], families));
        var snapshot = await connection.InvokeAsync<RunInfo?>(nameof(RunsHub.Subscribe), created.RunId);
        if (snapshot?.Status != RunStatus.Completed)
        {
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(90));
        }

        var result = await client.GetJsonAsync<BenchmarkResultDto>($"/api/runs/{created.RunId}/benchmark");
        var series = Assert.Single(Assert.Single(result.Families).Series);
        Assert.Equal(3, series.Points.Count);
        Assert.True(series.Points[0].Metrics.Repeatability > 0.99);
        Assert.True(snapshot?.Status == RunStatus.Completed || progress.Count > 0, "Expected progress events or an already completed run.");
    }

    [Fact]
    public async Task Comparison_run_matches_OpenCV()
    {
        Assert.True(OpenCvAvailability.IsAvailable, "The native OpenCV library is required.");
        using var client = factory.CreateClient();
        var image = await client.UploadAsync(Scene);

        var created = await client.PostRunAsync("/api/comparisons", new ComparisonRequest(image.Id, null));
        await client.WaitAsync(created.RunId);
        var result = await client.GetJsonAsync<ComparisonResultDto>($"/api/runs/{created.RunId}/comparison");

        Assert.True(result.Repeatability > 0.9, $"Repeatability {result.Repeatability:P1}.");
        Assert.True(result.MeanOffset < 0.5);
    }

    [Fact]
    public async Task Running_runs_can_be_canceled()
    {
        using var client = factory.CreateClient();
        var image = await client.UploadAsync(GoldenScene.Create(480, 360, seed: 99));
        var families = Transformations.DefaultValues.ToDictionary(p => p.Key, p => p.Value);

        var created = await client.PostRunAsync("/api/benchmarks", new BenchmarkRequest(image.Id, ["sift"], families));
        using var cancel = await client.DeleteAsync(new Uri($"/api/runs/{created.RunId}", UriKind.Relative));
        var run = await client.WaitAsync(created.RunId);

        Assert.Equal(HttpStatusCode.Accepted, cancel.StatusCode);
        Assert.Equal(RunStatus.Canceled, run.Status);
        using var again = await client.DeleteAsync(new Uri($"/api/runs/{created.RunId}", UriKind.Relative));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }
}
