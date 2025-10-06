using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzafyWeb.Models
{
    [Table("menu_price")]
    public class MenuPrice
    {
        [Key]
        [Column("price_id")]
        public int PriceId { get; set; }

        [Column("menu_item_id")]
        public int MenuItemId { get; set; }

        [Required]
        [Column("size_id")]
        public int SizeId { get; set; }

        [Required]
        [Column("unit_price")]
        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        // Navigation properties
        [ForeignKey("MenuItemId")]
        public virtual MenuItem MenuItem { get; set; } = null!;
        
        [ForeignKey("SizeId")]
        public virtual Size Size { get; set; } = null!;
    }
}