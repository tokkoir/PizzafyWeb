using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzafyWeb.Models
{
    [Table("user_addresses")]
    public class UserAddress
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("street_address")]
        public string StreetAddress { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("barangay")]
        public string Barangay { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Column("city")]
        public string City { get; set; } = "Cebu City";

        [MaxLength(255)]
        [Column("landmark")]
        public string? Landmark { get; set; }

        [Column("is_default")]
        public bool IsDefault { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}