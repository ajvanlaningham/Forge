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

    /// <summary>
    /// The design contract: 21 quests a week at 50 XP is exactly one level per week of full
    /// adherence, which is what makes "retest every 4 levels" land at roughly monthly.
    /// </summary>
    [Theory]
    [InlineData(1, 2)]   // after one perfect week
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    [InlineData(4, 5)]   // a test window opens here
    [InlineData(8, 9)]
    [InlineData(52, 53)] // a year of perfect adherence
    public void AWeekOfPerfectAdherence_IsExactlyOneLevel(int weeks, int expectedLevel)
    {
        var xp = weeks * GameMath.Quests.QuestsPerWeek * GameMath.Quests.XpPerQuest;
        Assert.Equal(expectedLevel, GameMath.LevelFromXp(xp));
        Assert.Equal(0, GameMath.XpIntoLevel(xp));
    }

    [Fact]
    public void EveryFourthLevel_OpensATestWindow()
    {
        // Walk a year one quest at a time and collect the XP totals where the level first
        // becomes a multiple of 4. These are the retest points.
        var windows = new List<int>();
        var lastLevel = 1;
        for (var quest = 1; quest <= GameMath.Quests.QuestsPerWeek * 52; quest++)
        {
            var xp = quest * GameMath.Quests.XpPerQuest;
            var level = GameMath.LevelFromXp(xp);
            if (level != lastLevel && level % 4 == 0) windows.Add(level);
            lastLevel = level;
        }

        Assert.Equal(new[] { 4, 8, 12, 16, 20, 24, 28, 32, 36, 40, 44, 48, 52 }, windows);
    }

    /// <summary>
    /// XP into the level and XP remaining must always account for exactly one level between
    /// them. If they drift apart the progress bar and the "next level" figure disagree.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1049)]
    [InlineData(1050)]
    [InlineData(5_432)]
    [InlineData(1_000_000)]
    public void XpIntoLevel_AndXpToNextLevel_AlwaysSumToOneLevel(int xp)
        => Assert.Equal(GameMath.XpPerLevel, GameMath.XpIntoLevel(xp) + GameMath.XpToNextLevel(xp));

    /// <summary>
    /// The regression this PBI existed to fix: the bar must sweep 0..1 within every level,
    /// not saturate after the first one.
    /// </summary>
    [Fact]
    public void ProgressBar_SweepsWithinEveryLevel_NotJustTheFirst()
    {
        foreach (var level in new[] { 0, 1, 2, 7 })
        {
            var levelStart = level * GameMath.XpPerLevel;

            Assert.Equal(0.0, GameMath.LevelProgress(GameMath.XpIntoLevel(levelStart)), 3);
            Assert.Equal(0.5, GameMath.LevelProgress(GameMath.XpIntoLevel(levelStart + 525)), 3);
            Assert.True(GameMath.LevelProgress(GameMath.XpIntoLevel(levelStart + 1049)) > 0.99);
        }
    }
}
