using Salient.Core;

namespace Salient.Core.Tests;

public sealed class BorderTests
{
    [Theory]
    [InlineData(0, 5, 0)]
    [InlineData(4, 5, 4)]
    [InlineData(-1, 5, 1)]
    [InlineData(-2, 5, 2)]
    [InlineData(5, 5, 3)]
    [InlineData(6, 5, 2)]
    [InlineData(-9, 5, 1)]
    [InlineData(13, 5, 3)]
    [InlineData(-3, 1, 0)]
    [InlineData(7, 2, 1)]
    public void Reflect101_mirrors_without_repeating_the_edge(int index, int length, int expected)
    {
        Assert.Equal(expected, Border.Reflect101(index, length));
    }
}
