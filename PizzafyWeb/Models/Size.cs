using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzafyWeb.Models
{
    [Table("size")]
    public class Size
    {
        [Key]
        [Column("size_id")]
        public int SizeId { get; set; }

        [Required]
        [Column("category_id")]
        public int CategoryId { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("size_name")]
        public string SizeName { get; set; } = string.Empty;

        // Navigation properties
        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; } = null!;
        
        public virtual ICollection<MenuPrice> MenuPrices { get; set; } = new List<MenuPrice>();
    }
}