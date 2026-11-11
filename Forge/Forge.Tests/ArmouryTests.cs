using Forge.Training;

namespace Forge.Tests;

public class ArmouryTests
{
    [Fact]
    public void Disciplines_ForgeTheirSlots()
    {
        Assert.Equal(GearSlot.Hearth, Armoury.SlotFor("Hearth"));
        Assert.Equal(GearSlot.Anvil, Armoury.SlotFor("Anvil"));
        Assert.Equal(GearSlot.Bellows, Armoury.SlotFor("Bellows"));
    }

    [Fact]
    public void Copper_IsAlwaysOpen_AndThreeSlotsOpenTheNextTier()
    {
        Assert.Equal(0, Armoury.OpenTier([]));
        var two = new[] { new Forged(GearSlot.Hearth, "Copper"), new Forged(GearSlot.Anvil, "copper"), new Forged(GearSlot.Anvil, "Copper") };
        Assert.Equal(0, Armoury.OpenTier(two));                     // the same slot twice is still two slots
        var three = two.Append(new Forged(GearSlot.Bellows, "Copper"));
        Assert.Equal(1, Armoury.OpenTier(three));                   // Bronze opens
        Assert.Equal("Bronze", Armoury.Metals[Armoury.OpenTier(three)]);
    }

    [Fact]
    public void Tiers_OpenInOrder()
    {
        // All three in Bronze but not Copper doesn't skip ahead.
        var bronze = Armoury.Slots.Select(s => new Forged(s, "Bronze"));
        Assert.Equal(0, Armoury.OpenTier(bronze));
    }

    [Fact]
    public void BestMetal_IsTheHighestForged()
    {
        var forged = new[] { new Forged(GearSlot.Anvil, "Copper"), new Forged(GearSlot.Anvil, "Iron"), new Forged(GearSlot.Hearth, "Bronze") };
        Assert.Equal("Iron", Armoury.BestMetal(forged, GearSlot.Anvil));
        Assert.Null(Armoury.BestMetal(forged, GearSlot.Bellows));
        Assert.Equal("iron", Armoury.Key("Iron"));
    }

    [Fact]
    public void EveryHero_HasANameForEverySlot()
    {
        string[] races = ["human", "elf", "dwarf", "orc", "halfling", "construct"];
        string[] classes = ["wizard", "warrior", "rogue", "ranger", "cleric", "monk"];
        var missing = (from r in races from c in classes from s in Armoury.Slots
                       where !HeroGear.Names.ContainsKey($"{r}_{c}_{Armoury.Key(s)}") select $"{r}_{c}_{s}").ToList();
        Assert.True(missing.Count == 0, "Missing gear names: " + string.Join(", ", missing));
        Assert.Equal("Archmage's Staff", HeroGear.Name("human", "wizard", GearSlot.Anvil));
        Assert.Equal("gear_human_wizard_anvil_copper.png", HeroGear.Sprite("human", "wizard", GearSlot.Anvil, "Copper"));
    }
}
