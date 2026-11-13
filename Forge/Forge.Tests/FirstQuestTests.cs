using Forge.Constants;
using Forge.Training;

namespace Forge.Tests;

public class FirstQuestTests
{
    [Theory]
    [InlineData("Hearth", true, false, 4, "copper-helm-db-4day")]
    [InlineData("Hearth", true, true, 3, "copper-helm-db-3day")]      // both: dumbbells first
    [InlineData("Anvil", false, true, 4, "copper-hammer-kb-4day")]    // kettlebells only
    [InlineData("Bellows", false, false, 4, "copper-boots-db-4day")]  // no gear: bodyweight swaps
    public void Suggests_TheVersionThatFits(string discipline, bool db, bool kb, int days, string id)
        => Assert.Equal(id, FirstQuest.ProgramId(discipline, db, kb, days));

    [Fact]
    public void EverySuggestion_IsAShippedProgram()
    {
        var shipped = GameConstants.Programs.Files.Select(f => Path.GetFileName(f).Split('.')[0]).ToHashSet();
        foreach (var d in new[] { "Hearth", "Anvil", "Bellows" })
            foreach (var (db, kb) in new[] { (true, false), (false, true), (false, false) })
                foreach (var days in new[] { 3, 4 })
                    Assert.Contains(FirstQuest.ProgramId(d, db, kb, days), shipped);
    }

    [Theory]
    [InlineData("Hearth")]
    [InlineData("Anvil")]
    [InlineData("Bellows")]
    public void Title_MatchesTheShippedProgram(string discipline)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Raw", "programs", FirstQuest.ProgramId(discipline, true, false, 4) + ".v1.json");
        var program = ProgramParser.Parse(File.ReadAllText(file));
        Assert.Equal(FirstQuest.Title(discipline), program.Name);
    }
}
