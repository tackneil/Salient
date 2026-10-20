namespace Salient.Api;

/// <summary>High-performance log messages (CA1848).</summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Run {RunId} failed")]
    public static partial void RunFailed(ILogger logger, Exception exception, Guid runId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Failed to notify subscribers of run {RunId}")]
    public static partial void NotifyFailed(ILogger logger, Exception? exception, Guid runId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Retention cleanup failed")]
    public static partial void RetentionFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Retention removed {Runs} runs and {Images} images")]
    public static partial void RetentionRemoved(ILogger logger, int runs, int images);
}
