using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PizzafyWeb.Pages
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly PizzafyDbContext _context;

        public ProfileModel(PizzafyDbContext context)
        {
            _context = context;
        }

        public string Username { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string MemberSince { get; set; } = string.Empty;

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required]
            [MaxLength(100)]
            public string FirstName { get; set; } = string.Empty;

            [Required]
            [MaxLength(100)]
            public string LastName { get; set; } = string.Empty;

            [Required]
            [RegularExpression(@"^(09\d{9}|\+639\d{9})$", ErrorMessage = "Please enter a valid 11-digit phone number (09XXXXXXXXX or +639XXXXXXXXX)")]
            public string PhoneNumber { get; set; } = string.Empty;

            [Required]
            [MaxLength(500)]
            public string Address { get; set; } = string.Empty;

            // Optional password change
            [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
            [MaxLength(50, ErrorMessage = "Password cannot exceed 50 characters")]
            public string? NewPassword { get; set; }

            [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
            public string? ConfirmPassword { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToPage("/Login");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            Username = user.Username;
            RoleName = user.UserType.ToString();
            MemberSince = user.CreatedAt.ToString("MMM dd, yyyy");

            Input = new InputModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Address = user.Address ?? string.Empty
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToPage("/Login");
            }

            if (!ModelState.IsValid)
            {
                // Keep summary data when redisplaying
                var existing = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
                if (existing != null)
                {
                    Username = existing.Username;
                    RoleName = existing.UserType.ToString();
                    MemberSince = existing.CreatedAt.ToString("MMM dd, yyyy");
                }
                return Page();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToPage();
            }

            user.FirstName = Input.FirstName.Trim();
            user.LastName = Input.LastName.Trim();
            user.PhoneNumber = Input.PhoneNumber.Trim();
            user.Address = Input.Address.Trim();

            // Only update password if provided (optional)
            if (!string.IsNullOrWhiteSpace(Input.NewPassword))
            {
                var newPass = Input.NewPassword.Trim();
                var confirm = (Input.ConfirmPassword ?? string.Empty).Trim();
                if (newPass != confirm)
                {
                    ModelState.AddModelError("Input.ConfirmPassword", "Passwords do not match");

                    // Keep summary data when redisplaying
                    Username = user.Username;
                    RoleName = user.UserType.ToString();
                    MemberSince = user.CreatedAt.ToString("MMM dd, yyyy");
                    return Page();
                }

                // Hash to match login/register behavior
                user.Password = HashPassword(newPass);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Profile updated successfully.";
            return RedirectToPage();
        }

        private string HashPassword(string password)
        {
            using (var md5 = MD5.Create())
            {
                var hashedBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToHexString(hashedBytes).ToLower();
            }
        }
    }
}
