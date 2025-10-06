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

namespace PizzafyWeb.Pages
{
    public class LoginModel : PageModel
    {
        private readonly PizzafyDbContext _context;

        public LoginModel(PizzafyDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Models.LoginModel Login { get; set; } = new Models.LoginModel();

        public void OnGet()
        {
            // Check if user is already logged in
            if (User.Identity!.IsAuthenticated)
            {
                Response.Redirect("/");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Hash the password for comparison (using simple MD5 for demonstration)
                var hashedPassword = HashPassword(Login.Password);

                // Find user in database
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == Login.Username && u.Password == hashedPassword);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "Invalid username or password.";
                    return Page();
                }

                // Create claims for the authenticated user
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

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(20)
                };

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authProperties);

                // Redirect based on user type
                if (user.UserType == UserType.Admin)
                {
                    return RedirectToPage("/Admin/MenuManagement");
                }
                else
                {
                    return RedirectToPage("/Index");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred during login. Please try again.";
                // Log the exception here if you have logging configured
                return Page();
            }
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