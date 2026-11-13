using SQLite;

namespace Forge.Models
{
    /// <summary>
    /// How a block ended at the forge: judged once, at the end of the block. Since Decision 25 a pass
    /// earns one forging, spent separately (<see cref="ForgingRow"/>); Slot and Metal are legacy
    /// columns from the metal × item quests and are no longer written.
    /// </summary>
    [Table("ForgeResult")]
    public sealed class ForgeResultRow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>The block judged. One result per block.</summary>
        [Indexed(Unique = true)]
        public int BlockId { get; set; }

        public string ProgramId { get; set; } = "";

        /// <summary>GearSlot as int.</summary>
        public int Slot { get; set; }

        /// <summary>"Copper"…"Starmetal".</summary>
        public string Metal { get; set; } = "Copper";

        /// <summary>True when the block made the 75%: a forging is earned. False: the metal cooled.</summary>
        public bool Forged { get; set; }

        public int DaysDone { get; set; }
        public int DaysNeeded { get; set; }
        public int DaysTotal { get; set; }

        /// <summary>yyyy-MM-dd the result was judged.</summary>
        public string DateKey { get; set; } = "";

        /// <summary>The forging moment has been shown.</summary>
        public bool Seen { get; set; }
    }
}
