using SQLite;

namespace Forge.Models
{
    /// <summary>
    /// How a block ended at the forge (Decision 18): judged once, at the end of the block. Records
    /// the slot and metal, not the item, so a forged piece re-skins when the hero changes (Decision 22).
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

        /// <summary>True when the block made the 75%: the piece is forged. False: the metal cooled.</summary>
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
