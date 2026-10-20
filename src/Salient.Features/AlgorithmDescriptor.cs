namespace Salient.Features;

/// <summary>What an algorithm can do; the UI uses these flags to decide which views and actions to offer.</summary>
[Flags]
public enum AlgorithmCapabilities
{
    /// <summary>No capabilities.</summary>
    None = 0,

    /// <summary>Detects keypoints.</summary>
    Detects = 1,

    /// <summary>Computes descriptors for keypoints.</summary>
    Describes = 2,

    /// <summary>Reports intermediate stages through <see cref="Pipeline.IPipelineObserver"/>.</summary>
    ExposesPipeline = 4,

    /// <summary>A third-party reference implementation, such as OpenCV.</summary>
    Reference = 8,
}

/// <summary>Static description of a registered algorithm.</summary>
/// <param name="Id">Stable identifier used in the API, for example <c>sift</c>.</param>
/// <param name="DisplayName">Name shown in the UI.</param>
/// <param name="Family">Algorithm family, for example <c>SIFT</c>; implementations of the same family can be compared.</param>
/// <param name="Capabilities">What the algorithm can do.</param>
/// <param name="ParametersType">The parameter record type; the API builds its JSON Schema from it.</param>
/// <param name="Version">Output version; bump it whenever the algorithm's results change so cached runs are invalidated (ADR-0008).</param>
public sealed record AlgorithmDescriptor(
    string Id,
    string DisplayName,
    string Family,
    AlgorithmCapabilities Capabilities,
    Type ParametersType,
    int Version);
