using Forge.Constants;
using Forge.Training;

namespace Forge.Tests;

public class ProgressionTests
{
    private static readonly Prescription ThreeBySixToTen = new(3, 6, 10, null);

    private static IReadOnlyList<WorkSet> Session(double load, params int[] reps)
        => reps.Select(r => new WorkSet(load, r, null)).ToList();

    private static Suggestion? Suggest(Prescription rx, params IReadOnlyList<WorkSet>[] history)
        => Progression.Suggest(rx, history, 5, WeightUnit.Pounds);

    [Fact]
    public void NoHistory_NoSuggestion()
        => Assert.Null(Suggest(ThreeBySixToTen));

    [Fact]
    public void EmptyLastSession_FallsBackToTheOneBefore()
        => Assert.Equal(SuggestionKind.IncreaseLoad, Suggest(ThreeBySixToTen, [], Session(100, 10, 10, 10))?.Kind);

    [Fact]
    public void AllSetsAtTopOfRange_AddsOneIncrement_AndResetsReps()
    {
        var s = Suggest(ThreeBySixToTen, Session(135, 10, 10, 10))!;
        Assert.Equal(SuggestionKind.IncreaseLoad, s.Kind);
        Assert.Equal(140, s.LoadLb);
        Assert.Equal(6, s.Reps);
    }

    [Fact]
    public void OneSetShortOfTop_HoldsLoad_ChasesAnotherRep()
    {
        var s = Suggest(ThreeBySixToTen, Session(135, 10, 10, 8))!;
        Assert.Equal(SuggestionKind.AddReps, s.Kind);
        Assert.Equal(135, s.LoadLb);
        Assert.Equal(9, s.Reps);
    }

    [Fact]
    public void PartialSession_NeverIncreases_EvenAtTopOfRange()
    {
        var s = Suggest(ThreeBySixToTen, Session(135, 10, 10))!;
        Assert.Equal(SuggestionKind.AddReps, s.Kind);
        Assert.Equal(135, s.LoadLb);
    }

    [Fact]
    public void MissedBottomOfRange_HoldsLoad()
    {
        var s = Suggest(ThreeBySixToTen, Session(155, 7, 6, 4))!;
        Assert.Equal(SuggestionKind.Hold, s.Kind);
        Assert.Equal(155, s.LoadLb);
        Assert.Equal(6, s.Reps);
    }

    [Fact]
    public void RepeatedMisses_HintAtDeload_ButNeverChangeTheLoad()
    {
        var s = Suggest(ThreeBySixToTen, Session(155, 6, 5, 4), Session(155, 6, 5, 5))!;
        Assert.Equal(SuggestionKind.ConsiderDeload, s.Kind);
        Assert.Equal(155, s.LoadLb); // a hint, not an action
    }

    [Fact]
    public void AMissAtADifferentLoad_DoesNotCountTowardTheDeloadHint()
        => Assert.Equal(SuggestionKind.Hold, Suggest(ThreeBySixToTen, Session(155, 6, 5, 4), Session(150, 6, 5, 5))?.Kind);

    [Fact]
    public void JudgesTheHeaviestLoad_IgnoringBackOffSets()
    {
        var mixed = new List<WorkSet> { new(135, 10, null), new(135, 10, null), new(135, 10, null), new(95, 15, null) };
        Assert.Equal(140, Suggest(ThreeBySixToTen, mixed)!.LoadLb);
    }

    [Fact]
    public void ChangedRepRange_IsJudgedAgainstTheCurrentPrescription()
    {
        // Logged 3x10 under an old 8-10 range; the program now says 10-12, so 10 is no longer the top.
        var s = Suggest(new Prescription(3, 10, 12, null), Session(135, 10, 10, 10))!;
        Assert.Equal(SuggestionKind.AddReps, s.Kind);
        Assert.Equal(11, s.Reps);
    }

    [Fact]
    public void Bodyweight_AtTopOfRange_SaysRepsHaveRunOut_InsteadOfAddingLoad()
    {
        var s = Suggest(new Prescription(3, 8, 12, null), Session(0, 12, 12, 12))!;
        Assert.Equal(SuggestionKind.BodyweightTopOfRange, s.Kind);
        Assert.Equal(0, s.LoadLb);
    }

    [Fact]
    public void Timed_AllSetsHeld_AddsTime()
    {
        var held = new List<WorkSet> { new(0, null, 45), new(0, null, 45), new(0, null, 50) };
        var s = Suggest(new Prescription(3, null, null, 45), held)!;
        Assert.Equal(SuggestionKind.AddTime, s.Kind);
        Assert.Equal(50, s.Seconds);
    }

    [Fact]
    public void Timed_ShortHold_KeepsTheTarget()
        => Assert.Equal(45, Suggest(new Prescription(3, null, null, 45),
            new List<WorkSet> { new(0, null, 45), new(0, null, 30), new(0, null, 45) })!.Seconds);

    [Fact]
    public void Kilograms_StepInKilograms()
    {
        // 60 kg stored as pounds, 2.5 kg step: next is 62.5 kg, not 60 kg + 5 lb.
        var lb = WeightMath.ToStoredPounds(60, WeightUnit.Kilograms);
        var s = Progression.Suggest(ThreeBySixToTen, [Session(lb, 10, 10, 10)], 2.5, WeightUnit.Kilograms)!;
        Assert.Equal(62.5, WeightMath.ToDisplay(s.LoadLb, WeightUnit.Kilograms), 6);
    }

    [Fact]
    public void OffGridLoad_StillGoesUp()
        => Assert.True(Progression.StepUp(137, 5, WeightUnit.Pounds) > 137);
}
