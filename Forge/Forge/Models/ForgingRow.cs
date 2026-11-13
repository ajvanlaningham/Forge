using SQLite;

namespace Forge.Models
{
    /// <summary>
    /// A forging spent (Decision 25): the passed block it came from, the slot the player chose and
    /// the metal it reached. One per passed block; a passed block without one is an unspent forging.
    /// Slot × metal, not the item, so pieces re-skin when the hero changes.
    /// </summary>
    [Table("Forging")]
    public sealed class ForgingRow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Unique = true)]
        public int BlockId { get; set; }

        /// <summary>GearSlot as int (Head 0 … Trinket 4).</summary>
        public int Slot { get; set; }

        /// <summary>"Copper"…"Starmetal": the level this forging raised the slot to.</summary>
        public string Metal { get; set; } = "Copper";

        /// <summary>The chosen form when the slot offers several; null for the hero's default.</summary>
        public string? Form { get; set; }

        /// <summary>yyyy-MM-dd it was spent.</summary>
        public string DateKey { get; set; } = "";
    }
}
