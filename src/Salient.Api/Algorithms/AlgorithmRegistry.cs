using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Salient.Core;
using Salient.Features;
using Salient.Features.Pipeline;
using Salient.Features.Sift;
using Salient.Reference.OpenCv;

namespace Salient.Api.Algorithms;

/// <summary>Keypoints and, when the algorithm describes, their descriptors.</summary>
public sealed record FeatureOutput(DetectionResult Detection, DescriptorSet? Descriptors);

/// <summary>Public description of an algorithm for <c>GET /api/algorithms</c>.</summary>
public sealed record AlgorithmInfo(
    string Id,
    string DisplayName,
    string Family,
    IReadOnlyList<string> Capabilities,
    int Version,
    JsonObject ParameterSchema);

/// <summary>A registered algorithm with type-erased entry points for the API.</summary>
public sealed class AlgorithmRegistration
{
    private readonly Func<JsonElement?, object> _parse;
    private readonly Func<object, object> _permissive;
    private readonly Func<ImageF, object, DetectionOptions, IPipelineObserver?, CancellationToken, FeatureOutput> _run;
    private readonly JsonSerializerOptions _json;

    private AlgorithmRegistration(
        AlgorithmDescriptor descriptor,
        JsonSerializerOptions json,
        Func<JsonElement?, object> parse,
        Func<object, object> permissive,
        Func<ImageF, object, DetectionOptions, IPipelineObserver?, CancellationToken, FeatureOutput> run)
    {
        Descriptor = descriptor;
        _json = json;
        _parse = parse;
        _permissive = permissive;
        _run = run;
        Info = new AlgorithmInfo(
            descriptor.Id,
            descriptor.DisplayName,
            descriptor.Family,
            Enum.GetValues<AlgorithmCapabilities>().Where(c => c != AlgorithmCapabilities.None && descriptor.Capabilities.HasFlag(c)).Select(c => c.ToString()).ToList(),
            descriptor.Version,
            ParameterSchema.For(descriptor.ParametersType, json));
    }

    public AlgorithmDescriptor Descriptor { get; }

    public AlgorithmInfo Info { get; }

    public bool Describes => Descriptor.Capabilities.HasFlag(AlgorithmCapabilities.Describes);

    /// <summary>Parses and validates parameters; missing values take their defaults. Throws <see cref="ValidationException"/>.</summary>
    public object Parse(JsonElement? json) => _parse(json);

    /// <summary>The parameters with thresholds widened for client-side exploration (ADR-0003); unchanged if the algorithm has none.</summary>
    public object Permissive(object parameters) => _permissive(parameters);

    /// <summary>Stable JSON for cache keys (ADR-0008).</summary>
    public string Canonical(object parameters) => JsonSerializer.Serialize(parameters, parameters.GetType(), _json);

    public FeatureOutput Run(ImageF image, object parameters, DetectionOptions options, IPipelineObserver? observer, CancellationToken cancellationToken) =>
        _run(image, parameters, options, observer, cancellationToken);

    public static AlgorithmRegistration Create<TParameters>(
        AlgorithmDescriptor descriptor,
        JsonSerializerOptions json,
        Func<ImageF, TParameters, DetectionOptions, IPipelineObserver?, CancellationToken, FeatureOutput> run,
        Func<TParameters, TParameters>? permissive = null)
        where TParameters : class, new()
    {
        return new AlgorithmRegistration(
            descriptor,
            json,
            element =>
            {
                TParameters parameters;
                try
                {
                    parameters = element is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } e
                        ? e.Deserialize<TParameters>(json) ?? new TParameters()
                        : new TParameters();
                }
                catch (JsonException ex)
                {
                    throw new ValidationException($"Invalid parameters: {ex.Message}");
                }

                Validator.ValidateObject(parameters, new ValidationContext(parameters), validateAllProperties: true);
                return parameters;
            },
            p => permissive is null ? p : permissive((TParameters)p),
            (image, p, options, observer, ct) => run(image, (TParameters)p, options, observer, ct));
    }
}

/// <summary>All algorithms available in this deployment (ADR-0003).</summary>
public sealed class AlgorithmRegistry
{
    private readonly Dictionary<string, AlgorithmRegistration> _byId;

    public AlgorithmRegistry(IEnumerable<AlgorithmRegistration> registrations)
    {
        All = registrations.ToList();
        _byId = All.ToDictionary(r => r.Descriptor.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<AlgorithmRegistration> All { get; }

    public AlgorithmRegistration? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Salient's SIFT plus, when the native library loads, the OpenCV references (ADR-0013).</summary>
    public static AlgorithmRegistry CreateDefault(JsonSerializerOptions json, bool includeOpenCv)
    {
        var sift = new SiftDetector();
        var registrations = new List<AlgorithmRegistration>
        {
            AlgorithmRegistration.Create<SiftParameters>(
                sift.Descriptor,
                json,
                (image, p, options, observer, ct) =>
                {
                    var (detection, descriptors) = sift.DetectAndCompute(image, p, options, observer, ct);
                    return new FeatureOutput(detection, descriptors);
                },
                p => p.Permissive()),
        };

        if (includeOpenCv && OpenCvAvailability.IsAvailable)
        {
            registrations.Add(Reference(new OpenCvSiftDetector(), json));
            registrations.Add(Reference(new OrbDetector(), json));
            registrations.Add(Reference(new AkazeDetector(), json));
        }

        return new AlgorithmRegistry(registrations);
    }

    private static AlgorithmRegistration Reference<TParameters>(OpenCvFeatureAlgorithm<TParameters> algorithm, JsonSerializerOptions json)
        where TParameters : class, new() =>
        AlgorithmRegistration.Create<TParameters>(
            algorithm.Descriptor,
            json,
            (image, p, _, observer, ct) =>
            {
                var (detection, descriptors) = algorithm.DetectAndCompute(image, p, observer, ct);
                return new FeatureOutput(detection, descriptors);
            });
}
