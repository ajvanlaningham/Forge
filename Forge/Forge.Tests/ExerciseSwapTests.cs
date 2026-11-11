using Forge.Models;
using Forge.Training;

namespace Forge.Tests;

public class ExerciseSwapTests
{
    private static ExerciseInfo Ex(string name, BodyZone zone, MovementPattern move, Modality mod, Equipment eq,
        ActionType action = ActionType.Reps, ExerciseCategory cat = ExerciseCategory.Strength)
        => new(name, cat, zone, move, mod, eq, action);

    private static readonly ExerciseInfo BarbellBench = Ex("Barbell Bench Press", BodyZone.Upper, MovementPattern.Push, Modality.Barbell, Equipment.Barbell | Equipment.Bench);
    private static readonly ExerciseInfo DbBench = Ex("Dumbbell Bench Press", BodyZone.Upper, MovementPattern.Push, Modality.Dumbbell, Equipment.Dumbbell | Equipment.Bench);
    private static readonly ExerciseInfo FloorPress = Ex("Dumbbell Floor Press", BodyZone.Upper, MovementPattern.Push, Modality.Dumbbell, Equipment.Dumbbell | Equipment.Mat);
    private static readonly ExerciseInfo PushUp = Ex("Push-up", BodyZone.Upper, MovementPattern.Push, Modality.Bodyweight, Equipment.None);
    private static readonly ExerciseInfo BarbellRow = Ex("Barbell Row", BodyZone.Upper, MovementPattern.Pull, Modality.Barbell, Equipment.Barbell);
    private static readonly ExerciseInfo Burpee = Ex("Burpee", BodyZone.FullBody, MovementPattern.Push, Modality.Bodyweight, Equipment.None, cat: ExerciseCategory.Conditioning);

    private static readonly ExerciseInfo[] Library = [BarbellBench, DbBench, FloorPress, PushUp, BarbellRow, Burpee];

    private const Equipment Home = Equipment.Dumbbell | Equipment.Mat | Equipment.Kettlebell;

    [Fact]
    public void AvailableExercise_ResolvesToItself()
        => Assert.Same(BarbellBench, ExerciseSwap.Resolve(BarbellBench, Library, Equipment.Barbell | Equipment.Bench));

    [Fact]
    public void MissingGear_SwapsToSameMovement_PreferringLoaded()
        // No bench at home: dumbbell bench is out, floor press (loaded) beats push-ups.
        => Assert.Equal("Dumbbell Floor Press", ExerciseSwap.Resolve(BarbellBench, Library, Home)?.Name);

    [Fact]
    public void Candidates_NeverCrossMovementPatternOrCategory()
    {
        var names = ExerciseSwap.Candidates(BarbellBench, Library, ~Equipment.None).Select(e => e.Name).ToList();
        Assert.DoesNotContain("Barbell Row", names);   // pull, not push
        Assert.DoesNotContain("Burpee", names);        // conditioning, not strength
        Assert.DoesNotContain("Barbell Bench Press", names); // not itself
        Assert.Equal(new[] { "Dumbbell Bench Press", "Dumbbell Floor Press", "Push-up" }, names);
    }

    [Fact]
    public void NoEquipmentAtAll_FallsBackToBodyweight()
        => Assert.Equal("Push-up", ExerciseSwap.Resolve(BarbellBench, Library, Equipment.None)?.Name);

    [Fact]
    public void SkipsSubstitutesAlreadyInTheSession()
    {
        var used = new HashSet<string> { "Dumbbell Floor Press" };
        Assert.Equal("Push-up", ExerciseSwap.Resolve(BarbellBench, Library, Home, used)?.Name);
    }

    [Fact]
    public void AllSubstitutesUsed_ResolvesToNull()
    {
        var used = new HashSet<string> { "Dumbbell Floor Press", "Push-up" };
        Assert.Null(ExerciseSwap.Resolve(BarbellBench, Library, Home, used));
    }

    [Fact]
    public void AvailablePrescription_IsKeptEvenIfAlreadyUsed()
        => Assert.Same(PushUp, ExerciseSwap.Resolve(PushUp, Library, Home, new HashSet<string> { "Push-up" }));

    [Fact]
    public void NothingQualifies_ResolvesToNull()
        => Assert.Null(ExerciseSwap.Resolve(BarbellRow, Library, Home));

    [Theory]
    [InlineData(Equipment.None, Equipment.None, true)]
    [InlineData(Equipment.Dumbbell, Equipment.Dumbbell | Equipment.Bench, true)]
    [InlineData(Equipment.Dumbbell | Equipment.Bench, Equipment.Dumbbell, false)]
    public void IsAvailable_RequiresEveryPiece(Equipment needs, Equipment have, bool expected)
        => Assert.Equal(expected, ExerciseSwap.IsAvailable(Ex("x", BodyZone.Upper, MovementPattern.Push, Modality.Dumbbell, needs), have));
}
