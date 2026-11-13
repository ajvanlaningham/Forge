using Forge.Training;

namespace Forge.Tests;

/// <summary>Decision 25: one quest, one forging; five slots climb the metal ladder independently.</summary>
public class ArmouryTests
{
    [Fact]
    public void FiveSlots_SevenMetals()
    {
        Assert.Equal(5, Armoury.Slots.Length);
        Assert.Equal(35, Armoury.MaxForgings);
    }

    [Fact]
    public void ABareSlot_ForgesCopperFirst()
    {
        Assert.Null(Armoury.MetalOf([], GearSlot.Head));
        Assert.Equal("Copper", Armoury.NextMetal([], GearSlot.Head));
    }

    [Fact]
    public void EachForging_RaisesItsSlotOneMetal_AndNoOther()
    {
        var forged = new[] { new Forged(GearSlot.Weapon, "Copper"), new Forged(GearSlot.Weapon, "Bronze"), new Forged(GearSlot.Feet, "Copper") };
        Assert.Equal("Bronze", Armoury.MetalOf(forged, GearSlot.Weapon));
        Assert.Equal("Iron", Armoury.NextMetal(forged, GearSlot.Weapon));
        Assert.Equal("Bronze", Armoury.NextMetal(forged, GearSlot.Feet));
        Assert.Equal("Copper", Armoury.NextMetal(forged, GearSlot.Body)); // no tier gate: any slot, any time
    }

    [Fact]
    public void Starmetal_IsTheTop()
    {
        var all = Armoury.Metals.Select(m => new Forged(GearSlot.Trinket, m)).ToList();
        Assert.Equal("Starmetal", Armoury.MetalOf(all, GearSlot.Trinket));
        Assert.Null(Armoury.NextMetal(all, GearSlot.Trinket));
        Assert.Equal(7, Armoury.Level(all.Append(new Forged(GearSlot.Trinket, "Starmetal")), GearSlot.Trinket));
    }

    [Fact]
    public void BestMetal_AndForms()
    {
        Assert.Null(Armoury.BestMetal([]));
        var forged = new[] { new Forged(GearSlot.Head, "Copper", "hood"), new Forged(GearSlot.Body, "Copper"), new Forged(GearSlot.Body, "Bronze") };
        Assert.Equal("Bronze", Armoury.BestMetal(forged));
        Assert.Equal("hood", Armoury.FormOf(forged, GearSlot.Head));
    }

    [Fact]
    public void Keys_AreLowercase()
    {
        Assert.Equal("iron", Armoury.Key("Iron"));
        Assert.Equal("weapon", Armoury.Key(GearSlot.Weapon));
    }

    [Fact]
    public void EveryHero_HasANameForEveryDrawnPiece()
    {
        string[] races = ["human", "elf", "dwarf", "orc", "halfling", "construct"];
        string[] classes = ["wizard", "warrior", "rogue", "ranger", "cleric", "monk"];
        string[] drawn = ["anvil", "hearth", "bellows"]; // Decision 22's art, borrowed until the five-slot set is drawn
        var missing = (from r in races from c in classes from k in drawn
                       where !HeroGear.Names.ContainsKey($"{r}_{c}_{k}") select $"{r}_{c}_{k}").ToList();
        Assert.True(missing.Count == 0, "Missing gear names: " + string.Join(", ", missing));
        Assert.Equal("Archmage's Staff", HeroGear.Name("human", "wizard", GearSlot.Weapon));
        Assert.Equal("gear_human_wizard_anvil_copper.png", HeroGear.Sprite("human", "wizard", GearSlot.Weapon, "Copper"));
        Assert.Null(HeroGear.Sprite("human", "wizard", GearSlot.Body, "Copper"));
        Assert.Equal("Armour", HeroGear.Name("human", "wizard", GearSlot.Body));
    }

    [Theory]
    [InlineData("human", "wizard", GearSlot.Head, "Starry Hat")]      // headwear
    [InlineData("dwarf", "cleric", GearSlot.Body, "Smith's Apron")]   // body wear
    [InlineData("human", "cleric", GearSlot.Trinket, "Holy Tome")]    // a trinket
    public void DefensivePieces_GoToTheSlotTheyFit(string race, string cls, GearSlot slot, string name)
    {
        Assert.Equal(slot, HeroGear.HearthSlotOf(race, cls));
        Assert.Equal(name, HeroGear.Name(race, cls, slot));
        Assert.Equal($"gear_{race}_{cls}_hearth_copper.png", HeroGear.Sprite(race, cls, slot, "Copper"));
    }

    [Fact]
    public void AShield_IsNeverShownAsAHelm()
    {
        Assert.Null(HeroGear.Sprite("human", "warrior", GearSlot.Head, "Copper"));
        Assert.Equal("Helm", HeroGear.Name("human", "warrior", GearSlot.Head));
    }
}
