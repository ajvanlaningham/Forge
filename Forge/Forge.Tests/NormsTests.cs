using Forge.Training;

namespace Forge.Tests;

public class NormsTests
{
    [Theory]
    [InlineData("men", 25, "Floor", 36, "Excellent")]
    [InlineData("men", 25, "Floor", 35, "Very good")]
    [InlineData("men", 44, "Floor", 14, "Good")]
    [InlineData("men", 44, "Floor", 9, "Needs improvement")]
    [InlineData("women", 33, "Knees", 20, "Very good")]
    [InlineData("women", 61, "Knees", 1, "Needs improvement")]
    public void PushUps_BandFromTheCsepTable(string group, int age, string version, int reps, string band)
        => Assert.Equal(band, Norms.PushUpBand(group, age, version, reps));

    [Theory]
    [InlineData("men", 44, "Hands on a box or step")]   // the men's table is for floor push-ups only
    [InlineData("women", 30, "Floor")]                   // the women's table is for knee push-ups
    [InlineData("men", 72, "Floor")]                     // outside 20–69
    [InlineData("none", 30, "Floor")]
    public void PushUps_NoBand_WhenTheTableDoesntApply(string group, int age, string version)
        => Assert.Null(Norms.PushUpBand(group, age, version, 20));
}
