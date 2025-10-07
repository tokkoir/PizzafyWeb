using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzafyWeb.Models
{
    [Table("order_detail")]
    public class OrderItem
    {
        [Key]
        [Column("order_detail_id")]
        public int OrderItemId { get; set; }

        [Required]
        [Column("order_id")]
        public int OrderId { get; set; }

        [Required]
        [Column("price_id")]
        public int PriceId { get; set; }

        [Required]
        [Column("quantity")]
        public int Quantity { get; set; }

        [Required]
        [Column("unit_price", TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [Column("line_total", TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }

        // Navigation properties
        [ForeignKey(nameof(OrderId))]
        public Order Order { get; set; } = null!;

        [ForeignKey(nameof(PriceId))]
        public MenuPrice MenuPrice { get; set; } = null!;
    }
}