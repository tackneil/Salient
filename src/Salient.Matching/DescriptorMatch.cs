namespace Salient.Matching;

/// <summary>The nearest and second-nearest train descriptor for one query descriptor.</summary>
/// <param name="QueryIndex">Index in the query set.</param>
/// <param name="TrainIndex">Index of the nearest descriptor in the train set.</param>
/// <param name="Distance">Distance to the nearest descriptor: L2 for real-valued descriptors, Hamming bits for binary ones.</param>
/// <param name="SecondDistance">Distance to the second-nearest descriptor, or positive infinity when the train set has one element.</param>
public readonly record struct DescriptorMatch(int QueryIndex, int TrainIndex, float Distance, float SecondDistance)
{
    /// <summary>
    /// Lowe's distance ratio d₁ / d₂ in [0, 1]. Low values mean a distinctive match. Defined as 0 when both distances are 0
    /// (an exact duplicate is as distinctive as it gets) and 1 when only the second is 0.
    /// </summary>
    public float Ratio => SecondDistance > 0 ? Distance / SecondDistance : (Distance > 0 ? 1f : 0f);
}
