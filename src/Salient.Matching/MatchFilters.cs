namespace Salient.Matching;

/// <summary>Filters that keep only reliable descriptor matches.</summary>
public static class MatchFilters
{
    /// <summary>
    /// Lowe's ratio test (2004, section 7.1): keeps matches whose nearest distance is clearly smaller than the second-nearest,
    /// d₁ &lt; <paramref name="ratio"/> · d₂. Lowe recommends 0.8.
    /// </summary>
    public static List<DescriptorMatch> RatioTest(IEnumerable<DescriptorMatch> matches, double ratio)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ratio);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ratio, 1.0);
        return matches.Where(m => m.Distance < ratio * m.SecondDistance).ToList();
    }

    /// <summary>
    /// Keeps forward matches (query → train) whose train descriptor's own nearest neighbour (train → query) is the same query.
    /// </summary>
    /// <param name="forward">Matches from query to train, as returned by <see cref="BruteForceMatcher.NearestTwo"/>.</param>
    /// <param name="backward">Matches from train to query, indexed by train descriptor.</param>
    public static List<DescriptorMatch> CrossCheck(IEnumerable<DescriptorMatch> forward, IReadOnlyList<DescriptorMatch> backward)
    {
        ArgumentNullException.ThrowIfNull(forward);
        ArgumentNullException.ThrowIfNull(backward);
        return forward.Where(m => m.TrainIndex < backward.Count && backward[m.TrainIndex].TrainIndex == m.QueryIndex).ToList();
    }
}
