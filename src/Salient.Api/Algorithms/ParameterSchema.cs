using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Salient.Features.Parameters;

namespace Salient.Api.Algorithms;

/// <summary>
/// Builds a JSON Schema for a parameter record from its attributes (ADR-0003): <see cref="RangeAttribute"/> gives
/// minimum and maximum, <see cref="StepAttribute"/> gives <c>x-step</c>, <see cref="DisplayAttribute"/> gives title,
/// description and <c>x-group</c>, <see cref="AdvancedAttribute"/> gives <c>x-advanced</c>. Defaults come from a new instance.
/// </summary>
public static class ParameterSchema
{
    public static JsonObject For(Type parametersType, JsonSerializerOptions jsonOptions)
    {
        ArgumentNullException.ThrowIfNull(parametersType);
        ArgumentNullException.ThrowIfNull(jsonOptions);
        var defaults = Activator.CreateInstance(parametersType)
            ?? throw new InvalidOperationException($"{parametersType} needs a parameterless constructor.");
        var properties = new JsonObject();
        var order = 0;

        foreach (var property in parametersType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite)
            {
                continue;
            }

            var name = jsonOptions.PropertyNamingPolicy?.ConvertName(property.Name) ?? property.Name;
            var schema = new JsonObject { ["type"] = JsonType(property.PropertyType), ["x-order"] = order++ };
            var display = property.GetCustomAttribute<DisplayAttribute>();
            if (display is not null)
            {
                schema["title"] = display.Name;
                if (display.Description is not null)
                {
                    schema["description"] = display.Description;
                }

                if (display.GroupName is not null)
                {
                    schema["x-group"] = display.GroupName;
                }
            }

            if (property.GetCustomAttribute<RangeAttribute>() is { } range)
            {
                schema["minimum"] = JsonValue.Create(Convert.ToDouble(range.Minimum, System.Globalization.CultureInfo.InvariantCulture));
                schema["maximum"] = JsonValue.Create(Convert.ToDouble(range.Maximum, System.Globalization.CultureInfo.InvariantCulture));
            }

            if (property.GetCustomAttribute<StepAttribute>() is { } step)
            {
                schema["x-step"] = step.Step;
            }

            if (property.GetCustomAttribute<AdvancedAttribute>() is not null)
            {
                schema["x-advanced"] = true;
            }

            if (property.PropertyType.IsEnum)
            {
                schema["enum"] = new JsonArray(Enum.GetNames(property.PropertyType).Select(n => (JsonNode)n).ToArray());
            }

            schema["default"] = JsonSerializer.SerializeToNode(property.GetValue(defaults), property.PropertyType, jsonOptions);
            properties[name] = schema;
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = properties,
        };
    }

    private static string JsonType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(bool))
        {
            return "boolean";
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short))
        {
            return "integer";
        }

        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        {
            return "number";
        }

        return "string";
    }
}
