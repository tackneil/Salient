using System.Text.Json;
using System.Text.Json.Serialization;

namespace Salient.Api;

/// <summary>JSON settings shared by HTTP, SignalR and stored results: web defaults with camelCase enum strings.</summary>
public static class ApiJson
{
    public static JsonSerializerOptions Options { get; } = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
