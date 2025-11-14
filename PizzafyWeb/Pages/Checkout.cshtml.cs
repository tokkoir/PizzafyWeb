using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using System.Security.Claims;

namespace PizzafyWeb.Pages
{
    [Authorize]
    public class CheckoutModel : PageModel
    {
        private readonly PizzafyDbContext _context;

        public CheckoutModel(PizzafyDbContext context)
        {
            _context = context;
        }

        public List<CartItemViewModel> CartItems { get; set; } = new();
        public decimal Subtotal => CartItems.Sum(i => i.Subtotal);
        public string CustomerFullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        public class CartItemViewModel
        {
            public int CartId { get; set; }
            public int PriceId { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public string SizeName { get; set; } = string.Empty;
            public string? Image { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Subtotal => UnitPrice * Quantity;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToPage("/Login");
            }

            CartItems = await _context.Carts
                .Where(c => c.UserId == userId)
                .Include(c => c.MenuPrice)
                    .ThenInclude(mp => mp.MenuItem)
                .Include(c => c.MenuPrice)
                    .ThenInclude(mp => mp.Size)
                .OrderBy(c => c.AddedAt)
                .Select(c => new CartItemViewModel
                {
                    CartId = c.CartId,
                    PriceId = c.PriceId,
                    ItemName = c.MenuPrice.MenuItem.ItemName,
                    SizeName = c.MenuPrice.Size.SizeName,
                    Image = c.MenuPrice.MenuItem.Image,
                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice
                })
                .ToListAsync();

            if (!CartItems.Any())
            {
                TempData["ErrorMessage"] = "Please add items to your cart before checking out.";
                return RedirectToPage("/Cart");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                PhoneNumber = user.PhoneNumber ?? string.Empty;
                CustomerFullName = ($"{user.FirstName} {user.LastName}").Trim();
            }

            return Page();
        }
    }
}
