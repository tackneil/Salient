using System.Threading.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Salient.Api;
using Salient.Api.Algorithms;
using Salient.Api.Images;
using Salient.Api.Runs;
using Salient.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SalientOptions>(builder.Configuration.GetSection(SalientOptions.Section));
builder.Services.ConfigureHttpJsonOptions(o => ApiJson.Configure(o.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddSignalR().AddJsonProtocol(o => ApiJson.Configure(o.PayloadSerializerOptions));
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(Endpoints.JobsRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = context.RequestServices.GetRequiredService<IOptions<SalientOptions>>().Value.JobsPerMinutePerClient,
            Window = TimeSpan.FromMinutes(1),
        }));
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 12 * 1024 * 1024);

builder.Services.AddSingleton<BlobStore>();
builder.Services.AddDbContextFactory<SalientDbContext>((services, options) =>
    options.UseSqlite($"Data Source={Path.Combine(services.GetRequiredService<BlobStore>().Root, "salient.db")}"));
builder.Services.AddSingleton(_ => AlgorithmRegistry.CreateDefault(ApiJson.Options, includeOpenCv: true));
builder.Services.AddSingleton<ImageService>();
builder.Services.AddSingleton<RunTracker>();
builder.Services.AddSingleton<RunDispatcher>();
builder.Services.AddSingleton<RunService>();
builder.Services.AddSingleton<RunExecutor>();
builder.Services.AddHostedService<RunWorker>();
builder.Services.AddHostedService<RetentionService>();

var app = builder.Build();

// The build-time OpenAPI generator starts the app only to read endpoint metadata; it must not touch storage.
var generatingOpenApi = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (!generatingOpenApi)
{
    app.Services.GetRequiredService<BlobStore>().EnsureRoot();
    await using var db = await app.Services.GetRequiredService<IDbContextFactory<SalientDbContext>>().CreateDbContextAsync();

    // v1 creates the schema directly; switch to EF migrations before the schema needs to evolve with data in place.
    await db.Database.EnsureCreatedAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseResponseCompression();
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapSalientApi();
app.MapHub<RunsHub>("/hubs/runs");
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Entry point; exposed so integration tests can host the API with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
