using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SQLite;

namespace Forge.Models
{
    /// <summary>
    /// Domain model (used by services/VMs).
    /// </summary>
    public class Exercise
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }

        public BodyZone BodyZone { get; set; }
        public MovementPattern Movement { get; set; }
        public Modality Modality { get; set; }
        public Equipment Equipment { get; set; }

        public ActionType Action { get; set; }

        // Default prescription (match to Action)
        public int? DefaultReps { get; set; }
        public int? DefaultSeconds { get; set; }
        public double? DefaultDistance { get; set; } // meters or km (we can add a unit later)

        public bool Unilateral { get; set; }
        public SkillLevel Skill { get; set; } = SkillLevel.Beginner;

        public bool IsActive { get; set; } = true;

        public ExerciseCategory Category { get; set; } = ExerciseCategory.Strength;

        // Only meaningful if Category == Mobility (okay to leave null otherwise)
        public MobilityTechnique? Technique { get; set; }

        // Breath‑based default 
        public int? DefaultBreaths { get; set; }
    }

    /// <summary>
    /// SQLite row (kept flat; enums persisted as ints).
    /// </summary>
    [Table("Exercise")]
    public class ExerciseRow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string? SourceTag { get; set; }  // e.g., "recovery" for recovery-only entries

        [Indexed(Name = "IX_Exercise_Name", Unique = true)]
        public string Name { get; set; } = "";

        public string? Description { get; set; }

        public int BodyZone { get; set; }
        public int Movement { get; set; }
        public int Modality { get; set; }
        public int Equipment { get; set; } // flags stored as int

        public int Action { get; set; }

        public int? DefaultReps { get; set; }
        public int? DefaultSeconds { get; set; }
        public double? DefaultDistance { get; set; }

        public bool Unilateral { get; set; }
        public int Skill { get; set; }

        public bool IsActive { get; set; } = true;

        public int Category { get; set; } // ExerciseCategory
        public int? Technique { get; set; } // MobilityTechnique?
        public int? DefaultBreaths { get; set; }

        // Mapping helpers
        public Exercise ToDomain() => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            BodyZone = (BodyZone)BodyZone,
            Movement = (MovementPattern)Movement,
            Modality = (Modality)Modality,
            Equipment = (Equipment)Equipment,
            Action = (ActionType)Action,
            DefaultReps = DefaultReps,
            DefaultSeconds = DefaultSeconds,
            DefaultDistance = DefaultDistance,
            Unilateral = Unilateral,
            Skill = (SkillLevel)Skill,
            IsActive = IsActive,

            Category = (ExerciseCategory)Category,
            Technique = Technique.HasValue ? (MobilityTechnique)Technique.Value : (MobilityTechnique?)null,
            DefaultBreaths = DefaultBreaths
        };

        public static ExerciseRow FromDomain(Exercise e) => new()
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            BodyZone = (int)e.BodyZone,
            Movement = (int)e.Movement,
            Modality = (int)e.Modality,
            Equipment = (int)e.Equipment,
            Action = (int)e.Action,
            DefaultReps = e.DefaultReps,
            DefaultSeconds = e.DefaultSeconds,
            DefaultDistance = e.DefaultDistance,
            Unilateral = e.Unilateral,
            Skill = (int)e.Skill,
            IsActive = e.IsActive,

            Category = (int)e.Category,
            Technique = e.Technique.HasValue ? (int)e.Technique.Value : (int?)null,
            DefaultBreaths = e.DefaultBreaths
        };

    }
}