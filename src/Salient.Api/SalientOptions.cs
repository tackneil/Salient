namespace Salient.Api;

/// <summary>Application settings, bound from the <c>Salient</c> configuration section.</summary>
public sealed class SalientOptions
{
    /// <summary>Configuration section name.</summary>
    public const string Section = "Salient";

    /// <summary>Folder for the SQLite database and blobs (ADR-0008). Relative paths are resolved against the content root.</summary>
    public string StorageRoot { get; set; } = "data";

    /// <summary>Folder with the bundled sample images, relative to the content root.</summary>
    public string SamplesPath { get; set; } = "samples";

    /// <summary>Largest accepted upload in bytes (ADR-0013).</summary>
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Uploads with more pixels are downscaled to this many (ADR-0009).</summary>
    public int MaxPixels { get; set; } = 4_000_000;

    /// <summary>Jobs executed in parallel.</summary>
    public int WorkerCount { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>Jobs that may wait in the queue; further requests get 503.</summary>
    public int QueueCapacity { get; set; } = 64;

    /// <summary>Runs and images older than this are deleted.</summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>Job creations allowed per client IP and minute.</summary>
    public int JobsPerMinutePerClient { get; set; } = 30;
}
