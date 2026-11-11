using Forge.Training;

namespace Forge.Tests;

public class ForgingTests
{
    [Theory]
    [InlineData(4, 16, 12)]
    [InlineData(3, 12, 9)]
    public void SeventyFivePercentOfTheBlock(int perWeek, int total, int needed)
    {
        Assert.Equal(total, Forging.TotalDays(perWeek));
        Assert.Equal(needed, Forging.Needed(perWeek));
        Assert.True(Forging.Forges(needed, perWeek));
        Assert.False(Forging.Forges(needed - 1, perWeek));
    }
}
