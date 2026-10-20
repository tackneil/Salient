using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Salient.Api.Images;
using Salient.Api.Runs;
using Salient.Core;

namespace Salient.Api.Tests;

/// <summary>Hosts the API with a throwaway storage folder.</summary>
public sealed class SalientApiFactory : WebApplicationFactory<Program>
{
    private readonly string _storage = Path.Combine(Path.GetTempPath(), "salient-tests", Guid.NewGuid().ToString("N"));

    public static JsonSerializerOptions Json { get; } = ApiJson.Options;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Salient:StorageRoot", _storage);
        builder.UseSetting("Salient:JobsPerMinutePerClient", "10000");
        builder.UseSetting("Salient:WorkerCount", "2");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            Directory.Delete(_storage, recursive: true);
        }
        catch (IOException)
        {
            // SQLite may still hold the file briefly; the temp folder is cleaned by the OS eventually.
        }
    }
}

internal static class ApiTestExtensions
{
    public static async Task<ImageInfo> UploadAsync(this HttpClient client, ImageF image, string name = "scene.png")
    {
        var png = ImageCodec.EncodePng(ImageCodec.FromGray(image.ToGray8(), image.Width, image.Height));
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", name);
        using var response = await client.PostAsync(new Uri("/api/images", UriKind.Relative), content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImageInfo>(SalientApiFactory.Json))!;
    }

    public static async Task<RunCreated> PostRunAsync<T>(this HttpClient client, string path, T request)
    {
        using var response = await client.PostAsJsonAsync(new Uri(path, UriKind.Relative), request, SalientApiFactory.Json);
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<RunCreated>(SalientApiFactory.Json))!;
    }

    public static async Task<RunInfo> WaitAsync(this HttpClient client, Guid runId, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(90));
        while (true)
        {
            var info = (await client.GetFromJsonAsync<RunInfo>(new Uri($"/api/runs/{runId}", UriKind.Relative), SalientApiFactory.Json))!;
            if (info.Status is RunStatus.Completed or RunStatus.Failed or RunStatus.Canceled)
            {
                return info;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Run {runId} is still {info.Status}.");
            }

            await Task.Delay(100);
        }
    }

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string path) =>
        (await client.GetFromJsonAsync<T>(new Uri(path, UriKind.Relative), SalientApiFactory.Json))!;
}
