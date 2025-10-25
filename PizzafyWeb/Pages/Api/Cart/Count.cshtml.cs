using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using System.Security.Claims;

namespace PizzafyWeb.Pages.Api.Cart
{
    [Authorize]
    public class CountModel : PageModel
    {
        private readonly PizzafyDbContext _context;

        public CountModel(PizzafyDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGet()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            // Get count of unique cart items (not total quantity)
            var uniqueItemCount = await _context.Carts
                .Where(c => c.UserId == userId)
                .CountAsync();

            return new JsonResult(new { count = uniqueItemCount });
        }
    }
}