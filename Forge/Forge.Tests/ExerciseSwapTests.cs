using System.Text.Json;
using Forge.Constants;
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

    [Fact]
    public void SamePlaneBeatsTheAlphabet()
    {
        // "Arnold Press" sorts first, but a bench press wants a horizontal press.
        var arnold = Ex("Arnold Press", BodyZone.Upper, MovementPattern.Push, Modality.Dumbbell, Equipment.Dumbbell) with { Plane = MovementPlane.Vertical };
        var floor = FloorPress with { Plane = MovementPlane.Horizontal };
        var bench = DbBench with { Plane = MovementPlane.Horizontal };
        Assert.Equal("Dumbbell Floor Press", ExerciseSwap.Resolve(bench, new[] { arnold, floor, bench }, Home)?.Name);
    }

    [Fact]
    public void ThePlainLiftBeatsItsHarderVariation()
    {
        var deficit = Ex("Deficit Push-up", BodyZone.Upper, MovementPattern.Push, Modality.Bodyweight, Equipment.None) with { Skill = SkillLevel.Intermediate };
        Assert.Equal("Push-up", ExerciseSwap.Resolve(DbBench, new[] { deficit, PushUp, DbBench }, Equipment.None)?.Name);
    }

    [Fact]
    public void NeverCrossesBodyZones()
    {
        var curl = Ex("Dumbbell Curl", BodyZone.Upper, MovementPattern.Flexion, Modality.Dumbbell, Equipment.Dumbbell);
        var crunch = Ex("Reverse Crunch", BodyZone.Core, MovementPattern.Flexion, Modality.Bodyweight, Equipment.Mat);
        Assert.Null(ExerciseSwap.Resolve(curl, new[] { curl, crunch }, Equipment.Mat));
    }

    // --- the shipped library: swaps people actually get ------------------------------------

    private static readonly Lazy<List<ExerciseInfo>> Shipped = new(() =>
    {
        static T Parse<T>(JsonElement e, string key, T fallback) where T : struct, Enum
            => e.TryGetProperty(key, out var v) && Enum.TryParse<T>(v.GetString(), true, out var r) ? r : fallback;
        var all = new List<ExerciseInfo>();
        foreach (var file in GameConstants.Exercises.LibraryFiles.Where(f => !f.Contains("recovery")))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Raw", file)));
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                var eq = Equipment.None;
                foreach (var x in e.GetProperty("equipment").EnumerateArray())
                    eq |= Enum.Parse<Equipment>(x.GetString()!, ignoreCase: true);
                all.Add(new ExerciseInfo(e.GetProperty("name").GetString()!,
                    Parse(e, "category", ExerciseCategory.Strength), Parse(e, "bodyZone", BodyZone.FullBody),
                    Parse(e, "movement", MovementPattern.Other), Parse(e, "modality", Modality.Bodyweight), eq,
                    Parse(e, "action", ActionType.Reps), Plane: Parse(e, "plane", MovementPlane.None),
                    Skill: Parse(e, "skill", SkillLevel.Beginner)));
            }
        }
        return all;
    });

    [Theory]
    // A home with dumbbells and a box but no bench (2026-11-12: Arnold Press briefly won this).
    [InlineData("Dumbbell Bench Press", Equipment.Dumbbell | Equipment.BoxStep | Equipment.Mat, "Dumbbell Floor Press")]
    [InlineData("Kettlebell Press", Equipment.Dumbbell | Equipment.Mat, "Dumbbell Overhead Press")]
    [InlineData("Kettlebell Deadlift", Equipment.Dumbbell | Equipment.Mat, "Romanian Deadlift")]
    [InlineData("Dumbbell Bench Press", Equipment.Mat, "Push-up")]
    [InlineData("Goblet Squat", Equipment.Dumbbell | Equipment.Mat, "Heel-Elevated Goblet Squat")]
    [InlineData("Lat Pulldown", Equipment.PullUpBar | Equipment.Bands, "Band-Assisted Pull-up")]
    public void ShippedLibrary_SwapsToTheObviousStandIn(string prescribed, Equipment home, string expected)
    {
        var lib = Shipped.Value;
        Assert.Equal(expected, ExerciseSwap.Resolve(lib.Single(e => e.Name == prescribed), lib, home)?.Name);
    }
}
