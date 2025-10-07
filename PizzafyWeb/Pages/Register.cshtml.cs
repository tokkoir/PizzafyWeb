using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using System.Security.Cryptography;
using System.Text;

namespace PizzafyWeb.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly PizzafyDbContext _context;

        public RegisterModel(PizzafyDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Models.RegisterModel Register { get; set; } = new Models.RegisterModel();

        public void OnGet()
        {
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

            if (Register.Password != Register.ConfirmPassword)
            {
                ModelState.AddModelError("Register.ConfirmPassword", "Passwords do not match");
                return Page();
            }

            try
            {
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == Register.Username);

                if (existingUser != null)
                {
                    TempData["ErrorMessage"] = "Username already exists. Please choose a different username.";
                    return Page();
                }

                var hashedPassword = HashPassword(Register.Password);

                var requestedRole = Register.RequestedRole;
                var userType = requestedRole == UserType.Admin ? UserType.Admin : UserType.Customer;
                var status = requestedRole == UserType.Admin ? AccountStatus.Pending : AccountStatus.Active;

                var newUser = new User
                {
                    Username = Register.Username,
                    FirstName = Register.FirstName,
                    LastName = Register.LastName,
                    Password = hashedPassword,
                    PhoneNumber = Register.PhoneNumber,
                    Address = Register.Address,
                    CreatedAt = DateTime.Now,
                    UserType = userType,
                    AccountStatus = status
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                if (requestedRole == UserType.Admin)
                {
                    TempData["SuccessMessage"] = "Your admin registration request was submitted and is pending approval by an administrator.";
                }
                else
                {
                    TempData["SuccessMessage"] = "Account created successfully! Please login with your credentials.";
                }

                return RedirectToPage("/Login");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while creating your account. Please try again.";
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