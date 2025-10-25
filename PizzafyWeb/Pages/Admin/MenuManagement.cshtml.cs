using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using System.Data.Common;

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

            try
            {
                // Try the normal EF query (will fail if DB doesn't have is_available column)
                MenuItems = await _context.MenuItems
                    .Include(m => m.Category)
                    .Include(m => m.MenuPrices)
                        .ThenInclude(mp => mp.Size)
                    .OrderBy(m => m.Category.CategoryName)
                    .ThenBy(m => m.ItemName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                // If the error is due to missing is_available column, fall back to manual SQL-based loading
                if (ex.Message != null && ex.Message.Contains("is_available", StringComparison.OrdinalIgnoreCase))
                {
                    await LoadMenuItemsWithoutIsAvailableAsync();
                }
                else
                {
                    throw; // rethrow unexpected errors
                }
            }
        }

        private async Task LoadMenuItemsWithoutIsAvailableAsync()
        {
            var conn = _context.Database.GetDbConnection();
            try
            {
                await conn.OpenAsync();
                // 1) Load menu items with category (do not reference is_available)
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT m.menu_item_id, m.item_name, m.description, m.image, m.category_id, c.category_name
                                         FROM menu_item m
                                         JOIN category c ON m.category_id = c.category_id
                                         ORDER BY c.category_name, m.item_name";
                    var list = new List<MenuItem>();
                    var ids = new List<int>();

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var item = new MenuItem
                            {
                                MenuItemId = reader.GetInt32(reader.GetOrdinal("menu_item_id")),
                                ItemName = reader.GetString(reader.GetOrdinal("item_name")),
                                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
                                Image = reader.IsDBNull(reader.GetOrdinal("image")) ? null : reader.GetString(reader.GetOrdinal("image")),
                                IsAvailable = true, // default when column missing
                                Category = new Category
                                {
                                    CategoryId = reader.GetInt32(reader.GetOrdinal("category_id")),
                                    CategoryName = reader.GetString(reader.GetOrdinal("category_name"))
                                }
                            };
                            list.Add(item);
                            ids.Add(item.MenuItemId);
                        }
                    }

                    // 2) Load menu prices and sizes for all retrieved menu item ids
                    if (ids.Any())
                    {
                        using (var pricesCmd = conn.CreateCommand())
                        {
                            // Build parameterized IN clause
                            var parameters = new List<string>();
                            for (int i = 0; i < ids.Count; i++)
                            {
                                var p = pricesCmd.CreateParameter();
                                p.ParameterName = "@id" + i;
                                p.Value = ids[i];
                                pricesCmd.Parameters.Add(p);
                                parameters.Add(p.ParameterName);
                            }

                            pricesCmd.CommandText = $@"SELECT p.price_id, p.menu_item_id, p.size_id, p.unit_price, s.size_name
                                                       FROM menu_price p
                                                       JOIN size s ON p.size_id = s.size_id
                                                       WHERE p.menu_item_id IN ({string.Join(',', parameters)})
                                                       ORDER BY p.menu_item_id, p.unit_price";

                            using (var reader = await pricesCmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var menuItemId = reader.GetInt32(reader.GetOrdinal("menu_item_id"));
                                    var menu = list.FirstOrDefault(x => x.MenuItemId == menuItemId);
                                    if (menu == null) continue;

                                    var price = new MenuPrice
                                    {
                                        PriceId = reader.GetInt32(reader.GetOrdinal("price_id")),
                                        MenuItemId = menuItemId,
                                        UnitPrice = reader.GetDecimal(reader.GetOrdinal("unit_price")),
                                        SizeId = reader.GetInt32(reader.GetOrdinal("size_id")),
                                        Size = new Size { SizeId = reader.GetInt32(reader.GetOrdinal("size_id")), SizeName = reader.GetString(reader.GetOrdinal("size_name")) }
                                    };
                                    menu.MenuPrices.Add(price);
                                }
                            }
                        }
                    }

                    // Assign to property
                    MenuItems = list;
                }
            }
            finally
            {
                try { await conn.CloseAsync(); } catch { }
            }
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
                    isAvailable = item.IsAvailable,
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

        public async Task<IActionResult> OnPostToggleAvailabilityAsync(int id)
        {
            try
            {
                // Ensure the is_available column exists in the database; if missing, add it so EF can update the value
                var conn = _context.Database.GetDbConnection();
                try
                {
                    await conn.OpenAsync();
                    using (var checkCmd = conn.CreateCommand())
                    {
                        checkCmd.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'menu_item' AND column_name = 'is_available'";
                        var existsObj = await checkCmd.ExecuteScalarAsync();
                        var exists = existsObj != null && Convert.ToInt32(existsObj) > 0;
                        if (!exists)
                        {
                            using var alterCmd = conn.CreateCommand();
                            alterCmd.CommandText = "ALTER TABLE menu_item ADD COLUMN is_available TINYINT(1) NOT NULL DEFAULT 1";
                            await alterCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
                finally
                {
                    try { await conn.CloseAsync(); } catch { }
                }

                // Proceed with normal EF update now that column exists
                var menuItem = await _context.MenuItems.FirstOrDefaultAsync(m => m.MenuItemId == id);
                if (menuItem == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return new JsonResult(new { ok = false, error = "Product not found" });
                    return RedirectToPage("/Admin/MenuManagement");
                }

                var targetName = menuItem.ItemName;
                var itemsWithSameName = await _context.MenuItems
                    .Where(m => m.ItemName == targetName)
                    .ToListAsync();

                var newValue = !menuItem.IsAvailable;
                foreach (var it in itemsWithSameName)
                {
                    it.IsAvailable = newValue;
                }
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = newValue ? "Product enabled successfully!" : "Product disabled successfully!";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { ok = true, isAvailable = newValue });
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while updating the product status.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { ok = false, error = ex.Message });
                }
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