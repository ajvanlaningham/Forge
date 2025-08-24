namespace Forge.Models
{
    public enum BodyZone
    {
        Upper = 0,
        Lower = 1,
        Core = 2,
        FullBody = 3
    }

    public enum MovementPattern
    {
        Squat = 0,
        Hinge = 1,
        Lunge = 2,
        Push = 3,
        Pull = 4,
        Carry = 5,
        Rotation = 6,
        AntiRotation = 7,
        Gait = 8,
        Other = 9,

        SpinalWave = 10,
        LateralFlexion = 11,
        KneeFlexion = 12,
        Flexion = 13,
        Extension = 14

    }

    public enum Modality
    {
        Bodyweight = 0,
        Dumbbell = 1,
        Kettlebell = 2,
        Barbell = 3,
        MachineBike = 4,
        Band = 5,
        Mobility = 6
    }

    public enum ActionType
    {
        Reps = 0,
        Time = 1,      // seconds
        Distance = 2,  // meters/km
        Hold = 3,      // isometric hold (seconds)
        Calories = 4   // erg machines
    }

    public enum SkillLevel
    {
        Beginner = 0,
        Intermediate = 1,
        Advanced = 2
    }

    [System.Flags]
    public enum Equipment : long
    {
        None = 0,
        Dumbbell = 1 << 0,
        Kettlebell = 1 << 1,
        Barbell = 1 << 2,
        Bench = 1 << 3,
        PullUpBar = 1 << 4,
        Bands = 1 << 5,
        Bike = 1 << 6,
        BoxStep = 1 << 7,
        Mat = 1 << 8,
        Wall = 1 << 9,
        JumpRope = 1 << 10,
        MedicineBall = 1 << 11,
        Sandbag = 1 << 12,
        Sled = 1 << 13,
        BattleRope = 1 << 14,
        DipStation = 1 << 15,
        RowMachine = 1 << 16,
        SkiErg = 1 << 17,
        WeightVest = 1 << 18,
        SuspensionTrainer = 1 << 19
    }

    public static class EquipmentGroups
    {
        private static readonly Dictionary<Equipment, string> _map = new()
        {
            { Equipment.Dumbbell, "Strength" },
            { Equipment.Kettlebell, "Strength" },
            { Equipment.Barbell, "Strength" },
            { Equipment.Bench, "Strength" },
            { Equipment.PullUpBar, "Strength" },
            { Equipment.DipStation, "Strength" },
            { Equipment.WeightVest, "Strength" },

            { Equipment.Bike, "Conditioning" },
            { Equipment.RowMachine, "Conditioning" },
            { Equipment.SkiErg, "Conditioning" },
            { Equipment.JumpRope, "Conditioning" },
            { Equipment.BattleRope, "Conditioning" },
            { Equipment.Sled, "Conditioning" },

            { Equipment.Bands, "Mobility" },
            { Equipment.Mat, "Mobility" },
            { Equipment.Wall, "Mobility" },
            { Equipment.SuspensionTrainer, "Mobility" },
            { Equipment.BoxStep, "Mobility" },
            { Equipment.MedicineBall, "Mobility" },
            { Equipment.Sandbag, "Mobility" }
        };

        public static string GetGroup(Equipment e) =>
            _map.TryGetValue(e, out var group) ? group : "Other";
    }

    public enum ExerciseCategory
    {
        Strength = 0,
        Conditioning = 1,
        Mobility = 2
    }

    public enum MobilityTechnique
    {
        StaticHold = 0,
        Dynamic = 1,
        PNF = 2,
        ContractRelax = 3,
        Flow = 4
    }
}
