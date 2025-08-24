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
    public enum Equipment
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
        JumpRope = 1 << 10
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
