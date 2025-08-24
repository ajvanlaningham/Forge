using System.Collections;
using Forge.Models;

namespace Forge.ViewModels.SubPages
{
    public static class ExerciseCategoryValues
    {
        public static IList All { get; } = new ArrayList
        {
            null,
            ExerciseCategory.Strength,
            ExerciseCategory.Conditioning,
            ExerciseCategory.Mobility
        };
    }

    public static class BodyZoneValues
    {
        public static IList All { get; } = new ArrayList
        {
            null,
            BodyZone.Upper,
            BodyZone.Lower,
            BodyZone.Core,
            BodyZone.FullBody
        };
    }
}
