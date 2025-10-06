using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PizzafyWeb.Pages
{
    public class LogoutModel : PageModel
    {
        public async Task<IActionResult> OnGet()
        {
            try
            {
                // Clear the authentication cookie
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                
                // Set a success message
                TempData["SuccessMessage"] = "You have been successfully logged out.";
            }
            catch (Exception)
            {
                // Set error message if something goes wrong
                TempData["ErrorMessage"] = "An error occurred during logout.";
            }
            
            return RedirectToPage("/Index");
        }
    }
}