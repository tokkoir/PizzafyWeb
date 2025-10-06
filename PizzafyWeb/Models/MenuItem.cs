using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzafyWeb.Models
{
    [Table("menu_item")]
    public class MenuItem
    {
        [Key]
        [Column("menu_item_id")]
        public int MenuItemId { get; set; }

        [Required]
        [Column("item_name")]
        [MaxLength(100)]
        public string ItemName { get; set; } = string.Empty;

        [Required]
        [Column("category_id")]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category Category { get; set; } = null!;

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;

        [Column("image")]
        [MaxLength(255)]
        public string? Image { get; set; } // Nullable, only set if uploaded

        public ICollection<MenuPrice> MenuPrices { get; set; } = new List<MenuPrice>();
    }
}