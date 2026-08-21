using Forge.Constants;

namespace Forge.Tests;

public class GameMathTests
{
    // One level == 21 quests at 50 XP == one week of full adherence.
    [Fact]
    public void XpPerLevel_IsAWeekOfQuests()
        => Assert.Equal(GameMath.XpPerLevel, GameMath.Quests.QuestsPerWeek * GameMath.Quests.XpPerQuest);

    [Theory]
    [InlineData(0, 1)]      // a brand new user starts at level 1, not 0
    [InlineData(1049, 1)]   // one XP short of the boundary
    [InlineData(1050, 2)]   // exactly on the boundary
    [InlineData(1051, 2)]
    [InlineData(2100, 3)]
    public void LevelFromXp_CrossesAtTheBoundary(int xp, int expected)
        => Assert.Equal(expected, GameMath.LevelFromXp(xp));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1049, 1049)]
    [InlineData(1050, 0)]   // resets at the boundary
    [InlineData(1051, 1)]
    public void XpIntoLevel_ResetsEachLevel(int xp, int expected)
        => Assert.Equal(expected, GameMath.XpIntoLevel(xp));

    [Theory]
    [InlineData(0, 1050)]
    [InlineData(1049, 1)]
    [InlineData(1050, 1050)]
    [InlineData(1051, 1049)]
    public void XpToNextLevel_CountsDownToTheBoundary(int xp, int expected)
        => Assert.Equal(expected, GameMath.XpToNextLevel(xp));

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(525, 0.5)]
    [InlineData(1050, 1.0)]
    public void LevelProgress_IsTheFractionOfOneLevel(int xpIntoLevel, double expected)
        => Assert.Equal(expected, GameMath.LevelProgress(xpIntoLevel), 3);

    [Fact]
    public void LevelProgress_ClampsRatherThanExceedingOne()
        => Assert.Equal(1.0, GameMath.LevelProgress(99_999), 3);

    /// <summary>
    /// The composition callers should use: progress within the CURRENT level, not a fraction
    /// of lifetime XP. Passing a lifetime total pins the bar at 100% forever once the first
    /// level is cleared — see PBI 4.1 in TODO.md.
    /// </summary>
    [Fact]
    public void LevelProgress_ComposedWithXpIntoLevel_TracksWithinTheCurrentLevel()
    {
        const int lifetimeXp = 1575; // level 2, halfway to level 3

        Assert.Equal(2, GameMath.LevelFromXp(lifetimeXp));
        Assert.Equal(0.5, GameMath.LevelProgress(GameMath.XpIntoLevel(lifetimeXp)), 3);
        Assert.Equal(1.0, GameMath.LevelProgress(lifetimeXp), 3); // the pinned-bar behaviour
    }
}
