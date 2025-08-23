namespace Forge.Models
{
    public enum StatKind
    {
        Strength,   // STR: max force (we’ll use e1RM/BW)
        Dexterity,  // DEX: mobility/control (deep squat hold time)
        Constitution // CON: endurance/resilience (10-min distance for now)
    }

    // Domain model (not a DB table)
    public class Stat
    {
        public StatKind Kind { get; init; }
        public double Baseline { get; init; } = 0;  // domain metric, optional
        public double Current { get; init; } = 0;  // domain metric, optional
        public string Unit { get; init; } = "";

        // Start users at 1 (not 10). Clamp logic lives in service.
        public int Score { get; init; } = 1;
    }
}