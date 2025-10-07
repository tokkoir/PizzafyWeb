using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;

namespace PizzafyWeb.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class MenuManagementModel : PageModel
    {
        private readonly PizzafyDbContext _context;

        public MenuManagementModel(PizzafyDbContext context)
        {
            _context = context;
        }

        public List<MenuItem> MenuItems { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
        public string CurrentCategory { get; set; } = "All Categories";

        public async Task OnGetAsync(string? category = null)
        {
            // Load categories for filter tabs
            Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            CurrentCategory = string.IsNullOrEmpty(category) ? "All Categories" : category;

            // Always load all menu items for client-side filtering
            MenuItems = await _context.MenuItems
                .Include(m => m.Category)
                .Include(m => m.MenuPrices)
                    .ThenInclude(mp => mp.Size)
                .OrderBy(m => m.Category.CategoryName)
                .ThenBy(m => m.ItemName)
                .ToListAsync();
        }

        public async Task<IActionResult> OnGetRefreshProductsAsync()
        {
            try
            {
                var menuItems = await _context.MenuItems
                    .Include(m => m.Category)
                    .Include(m => m.MenuPrices)
                        .ThenInclude(mp => mp.Size)
                    .OrderBy(m => m.Category.CategoryName)
                    .ThenBy(m => m.ItemName)
                    .ToListAsync();

                var result = menuItems.Select(item => new
                {
                    menuItemId = item.MenuItemId,
                    itemName = item.ItemName,
                    categoryName = item.Category.CategoryName,
                    image = item.Image,
                    imagePath = GetImagePath(item),
                    prices = item.MenuPrices.Select(p => new
                    {
                        sizeName = p.Size.SizeName,
                        unitPrice = p.UnitPrice
                    }).OrderBy(p => p.unitPrice).ToList()
                }).ToList();

                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                var menuItem = await _context.MenuItems.FindAsync(id);
                if (menuItem != null)
                {
                    _context.MenuItems.Remove(menuItem);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Product deleted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Product not found.";
                }
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "An error occurred while deleting the product.";
            }

            return RedirectToPage("/Admin/MenuManagement");
        }

        private string GetImagePath(MenuItem item)
        {
            if (string.IsNullOrEmpty(item.Image))
            {
                var category = item.Category?.CategoryName?.ToLower();
                if (category == "beverages")
                    return "/images/beverages/beverage-placeholder.svg";
                // Pizza and sides use the same placeholder
                if (category == "pizza" || category == "sides")
                    return "/images/product-placeholder.svg";
                return "/images/product-placeholder.svg";
            }
            return $"/images/products/{item.Image}?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        }
    }
}