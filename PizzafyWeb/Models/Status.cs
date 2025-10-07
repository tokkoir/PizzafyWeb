using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzafyWeb.Models
{
    [Table("status")]
    public class Status
    {
        [Key]
        [Column("status_id")]
        public int StatusId { get; set; }

        [Required]
        [Column("status_name")]
        [MaxLength(50)]
        public string StatusName { get; set; } = string.Empty;

        [Column("description")]
        [MaxLength(200)]
        public string? Description { get; set; }

        // Navigation properties
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}