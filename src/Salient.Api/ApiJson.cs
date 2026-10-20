using System.Text.Json;
using System.Text.Json.Serialization;

namespace Salient.Api;

/// <summary>
/// JSON settings shared by HTTP, SignalR and stored results: web defaults with camelCase enum strings and strict numbers.
/// Strict number handling keeps the OpenAPI schema (and the generated TypeScript types) to plain numbers instead of
/// "number or numeric string".
/// </summary>
public static class ApiJson
{
    public static JsonSerializerOptions Options { get; } = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.NumberHandling = JsonNumberHandling.Strict;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
