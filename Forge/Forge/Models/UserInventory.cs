using SQLite;

namespace Forge.Models
{
    [Table("UserInventory")]
    public class UserInventory
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed(Name = "IX_Inventory_Equipment", Unique = true)]
        public Equipment Equipment { get; set; }

        public bool Owned { get; set; } = true;
    }
}
