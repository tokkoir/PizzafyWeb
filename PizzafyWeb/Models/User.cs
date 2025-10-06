using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PizzafyWeb.Models
{
    [Table("user")]
    public class User
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("username")]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [Column("user_fname")]
        [MaxLength(30)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Column("user_lname")]
        [MaxLength(30)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Column("password")]
        [MaxLength(50)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Column("phone_number")]
        [MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [Column("address")]
        [MaxLength(100)]
        public string Address { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        [Column("user_type")]
        public UserType UserType { get; set; } = UserType.Customer;
    }

    public enum UserType
    {
        Customer,
        Admin
    }
}