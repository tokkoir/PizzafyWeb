using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;

namespace PizzafyWeb.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class UserApprovalsModel : PageModel
    {
        private readonly PizzafyDbContext _db;

        public UserApprovalsModel(PizzafyDbContext db)
        {
            _db = db;
        }

        public List<User> PendingAdmins { get; set; } = new();

        public async Task OnGetAsync()
        {
            PendingAdmins = await _db.Users
                .Where(u => u.UserType == UserType.Admin && u.AccountStatus == AccountStatus.Pending)
                .OrderBy(u => u.CreatedAt)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToPage();
            }

            if (user.UserType != UserType.Admin)
            {
                TempData["ErrorMessage"] = "Only admin accounts require approval.";
                return RedirectToPage();
            }

            user.AccountStatus = AccountStatus.Active;
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Approved admin account: {user.Username}.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeclineAsync(int id)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToPage();
            }

            if (user.UserType != UserType.Admin)
            {
                TempData["ErrorMessage"] = "Only admin accounts require approval.";
                return RedirectToPage();
            }

            user.AccountStatus = AccountStatus.Disabled;
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Declined admin account: {user.Username}.";
            return RedirectToPage();
        }
    }
}
