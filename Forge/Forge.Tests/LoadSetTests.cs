using Forge.Constants;
using Forge.Models;
using Forge.Training;

namespace Forge.Tests;

public class LoadSetTests
{
    [Fact]
    public void Discrete_NextAndPrev_StepThroughOwnedWeights()
    {
        var pairs = new LoadSet.Discrete([35, 15, 25, 20, 50]);
        Assert.Equal(35, pairs.Next(25));
        Assert.Equal(20, pairs.Prev(25));
        Assert.Null(pairs.Next(50));
        Assert.Null(pairs.Prev(15));
        Assert.Equal(15, pairs.Next(0));
        Assert.Equal(25, pairs.Snap(27));
        Assert.Equal(50, pairs.Max);
    }

    [Fact]
    public void Barbell_StepsFromTheBar()
    {
        var bar = new LoadSet.Range(45, 400, 5, WeightUnit.Pounds);
        Assert.Equal(45, bar.Next(0));      // nothing lighter than the empty bar
        Assert.Equal(140, bar.Next(135));
        Assert.Equal(130, bar.Prev(135));
        Assert.Null(bar.Prev(45));
        Assert.Null(bar.Next(400));
        Assert.Equal(135, bar.Snap(136));
        Assert.Equal(45, bar.Snap(20));
    }

    [Fact]
    public void Range_OffGridMax_SnapsDownToTheLastReachableLoad()
    {
        var bar = new LoadSet.Range(45, 402, 5, WeightUnit.Pounds);
        Assert.Equal(400, bar.Max);
        Assert.Equal(400, bar.Snap(405));
    }

    [Fact]
    public void Range_OffGridCurrent_NextStillGoesUp()
        => Assert.Equal(140, new LoadSet.Range(45, null, 5, WeightUnit.Pounds).Next(137));

    [Fact]
    public void KgRange_StepsInKilograms()
    {
        var kg = (double v) => WeightMath.ToStoredPounds(v, WeightUnit.Kilograms);
        var bar = new LoadSet.Range(kg(20), null, 2.5, WeightUnit.Kilograms);
        Assert.Equal(62.5, WeightMath.ToDisplay(bar.Next(kg(60))!.Value, WeightUnit.Kilograms), 6);
    }

    [Fact]
    public void Gear_UnspecifiedHomeDumbbells_FallBackToPlainSteps()
        => Assert.Equal(30, new GearLoads().For(Modality.Dumbbell, WeightUnit.Pounds).Next(25));

    [Fact]
    public void Gear_FixedPairs_AreDiscrete()
    {
        var gear = new GearLoads { Dumbbells = new DumbbellGear { Weights = [10, 20, 30] } };
        Assert.Equal(30, gear.For(Modality.Dumbbell, WeightUnit.Pounds).Next(20));
    }

    [Fact]
    public void Gear_KgKettlebells_StayExactWhenDisplayedInPounds()
    {
        var gear = new GearLoads { Kettlebells = new KettlebellGear { Unit = WeightUnit.Kilograms, Weights = [16, 24] } };
        var next = gear.For(Modality.Kettlebell, WeightUnit.Pounds).Next(WeightMath.ToStoredPounds(16, WeightUnit.Kilograms))!.Value;
        Assert.Equal(24, WeightMath.ToDisplay(next, WeightUnit.Kilograms), 6);
    }

    [Fact]
    public void Gym_HasEverything_NoMaxOnDumbbellsOrBarbell()
    {
        var gym = GearLoads.Everything(WeightUnit.Pounds);
        Assert.Null(gym.For(Modality.Dumbbell, WeightUnit.Pounds).Max);
        Assert.Null(gym.For(Modality.Barbell, WeightUnit.Pounds).Max);
        Assert.Equal(410, gym.For(Modality.Barbell, WeightUnit.Pounds).Next(405));
    }

    [Fact]
    public void Gear_RoundTripsThroughJson()
    {
        var gear = new GearLoads
        {
            Dumbbells = new DumbbellGear { Adjustable = true, Min = 5, Max = 52.5, Step = 2.5 },
            Barbell = new BarbellGear { Bar = 45, Max = 400, Step = 5 }
        };
        var back = GearLoads.FromJson(gear.ToJson());
        Assert.True(back.Dumbbells!.Adjustable);
        Assert.Equal(52.5, back.Dumbbells.Max);
        Assert.Equal(400, back.Barbell!.Max);
        Assert.Null(back.Kettlebells);
    }

    [Fact]
    public void Gear_BadJson_IsEmptyNotACrash()
        => Assert.Null(GearLoads.FromJson("{ not json").Dumbbells);
}
