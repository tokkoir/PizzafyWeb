using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using System.Security.Claims;
using PizzafyWeb.Utils; // tracker

namespace PizzafyWeb.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class UsersModel : PageModel
    {
        private readonly PizzafyDbContext _context;
        public UsersModel(PizzafyDbContext context) { _context = context; }

        public List<UserRow> Admins { get; set; } = new();
        public List<UserRow> Customers { get; set; } = new();
        public int? CurrentUserId { get; set; }

        public class UserRow
        {
            public int UserId { get; set; }
            public string Username { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public UserType UserType { get; set; }
            public AccountStatus AccountStatus { get; set; }
        }

        public async Task OnGetAsync()
        {
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) CurrentUserId = uid;

            var users = await _context.Users
                .OrderBy(u => u.UserType)
                .ThenBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ToListAsync();

            var rows = users.Select(u => new UserRow
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = $"{u.FirstName} {u.LastName}",
                CreatedAt = u.CreatedAt,
                UserType = u.UserType,
                AccountStatus = u.AccountStatus
            }).ToList();

            Admins = rows.Where(r => r.UserType == UserType.Admin).ToList();
            Customers = rows.Where(r => r.UserType == UserType.Customer).ToList();
        }

        public async Task<IActionResult> OnPostSetStatusAsync(int userId, AccountStatus newStatus)
        {
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) && uid == userId)
            {
                TempData["ErrorMessage"] = "You cannot change your own account status.";
                return RedirectToPage();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToPage();
            }

            var prevStatus = user.AccountStatus;
            user.AccountStatus = newStatus;
            await _context.SaveChangesAsync();

            // If an admin was Disabled and is now Active, mark for login-time pending conversion
            if (user.UserType == UserType.Admin && prevStatus == AccountStatus.Disabled && newStatus == AccountStatus.Active)
            {
                AdminReenableTracker.Mark(user.UserId);
                TempData["SuccessMessage"] = $"{user.Username} re-enabled. Will require approval on next login attempt.";
            }
            else
            {
                TempData["SuccessMessage"] = $"Updated status for {user.Username} to {newStatus}.";
            }
            return RedirectToPage();
        }
    }
}
