using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using PizzafyWeb.Utils;

namespace PizzafyWeb.Pages
{
    public class LoginModel : PageModel
    {
        private readonly PizzafyDbContext _context;
        public LoginModel(PizzafyDbContext context) { _context = context; }

        [BindProperty]
        public Models.LoginModel Login { get; set; } = new Models.LoginModel();

        public void OnGet()
        {
            if (User.Identity!.IsAuthenticated) Response.Redirect("/");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();
            try
            {
                var username = (Login.Username ?? string.Empty).Trim();
                var rawPassword = (Login.Password ?? string.Empty).Trim();
                var hashedPassword = HashPassword(rawPassword);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username && u.Password == hashedPassword);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "Invalid username or password.";
                    return Page();
                }

                if (user.AccountStatus == AccountStatus.Disabled)
                {
                    TempData["ErrorMessage"] = "Your account has been disabled. Please contact support.";
                    return Page();
                }

                // If admin was re-enabled, force Pending once at first login
                if (user.UserType == UserType.Admin && user.AccountStatus == AccountStatus.Active && AdminReenableTracker.Consume(user.UserId))
                {
                    user.AccountStatus = AccountStatus.Pending;
                    await _context.SaveChangesAsync();
                }

                if (user.UserType == UserType.Admin && user.AccountStatus == AccountStatus.Pending)
                {
                    TempData["ErrorMessage"] = "Your admin account requires approval before you can access the dashboard.";
                    return Page();
                }

                if (user.UserType == UserType.Customer && user.AccountStatus == AccountStatus.Pending)
                {
                    user.AccountStatus = AccountStatus.Active;
                    await _context.SaveChangesAsync();
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.GivenName, user.FirstName),
                    new Claim(ClaimTypes.Surname, user.LastName),
                    new Claim(ClaimTypes.Role, user.UserType.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                var authProperties = new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(20) };
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authProperties);

                return user.UserType == UserType.Admin ? RedirectToPage("/Admin/Dashboard") : RedirectToPage("/Index");
            }
            catch
            {
                TempData["ErrorMessage"] = "An error occurred during login. Please try again.";
                return Page();
            }
        }

        private string HashPassword(string password)
        {
            using var md5 = MD5.Create();
            var hashedBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(hashedBytes).ToLower();
        }
    }
}