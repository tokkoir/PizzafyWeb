using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzafyWeb.Models
{
    [Table("orders")]
    public class Order
    {
        [Key]
        [Column("order_id")]
        public int OrderId { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("status_id")]
        public int StatusId { get; set; }

        [Required]
        [Column("payment_id")]
        public int PaymentId { get; set; }

        [Column("order_date")]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("total_amount", TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Column("delivery_fee", TypeName = "decimal(10,2)")]
        public decimal DeliveryFee { get; set; } = 19m;

        [Column("delivery_address")]
        [MaxLength(500)]
        public string? DeliveryAddress { get; set; }

        [Column("last_update")]
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

        [Column("modified_by")]
        public int? ModifiedBy { get; set; }

        // Navigation properties
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [ForeignKey(nameof(StatusId))]
        public Status Status { get; set; } = null!;

        [ForeignKey(nameof(PaymentId))]
        public Payment Payment { get; set; } = null!;

        [ForeignKey(nameof(ModifiedBy))]
        public User? ModifiedByUser { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
