using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using System.Security.Claims;

namespace PizzafyWeb.Pages
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class CartModel : PageModel
    {
        private readonly PizzafyDbContext _context;
        private readonly CartSettings _cartSettings;
        
        public CartModel(PizzafyDbContext context, IOptions<CartSettings> cartSettings) 
        { 
            _context = context; 
            _cartSettings = cartSettings.Value;
        }

        public List<CartRowVm> Items { get; set; } = new();
        public decimal Total => Items.Sum(i => i.Subtotal);

        /// <summary>
        /// Helper method to format price with peso symbol
        /// </summary>
        private static string FormatPrice(decimal price)
        {
            // Use Unicode escape sequence for peso symbol to ensure compatibility
            return $"\u20B1{price:F2}";
        }

        /// <summary>
        /// Merges duplicate cart items for the specified user
        /// </summary>
        private async Task<int> MergeDuplicateCartItemsAsync(int userId)
        {
            var allCartItems = await _context.Carts
                .Where(c => c.UserId == userId)
                .ToListAsync();

            var duplicateGroups = allCartItems
                .GroupBy(c => c.PriceId)
                .Where(g => g.Count() > 1)
                .ToList();

            int mergedCount = 0;

            if (duplicateGroups.Any())
            {
                foreach (var group in duplicateGroups)
                {
                    // Keep the first row, sum quantities, remove the rest
                    var firstRow = group.OrderBy(c => c.CartId).First();
                    var totalQty = group.Sum(c => c.Quantity);
                    firstRow.Quantity = totalQty;
                    
                    var duplicates = group.Skip(1).ToList();
                    mergedCount += duplicates.Count;
                    _context.Carts.RemoveRange(duplicates);
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Ignore if rows were merged/removed concurrently
                }
            }

            return mergedCount;
        }

        public class CartRowVm
        {
            public int CartId { get; set; }
            public int MenuItemId { get; set; }
            public int CurrentPriceId { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public string CategoryName { get; set; } = string.Empty;
            public string? Image { get; set; }
            public string SizeName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal CurrentPrice { get; set; } // Current price from menu
            public bool PriceChanged { get; set; } // Flag to indicate price change
            public decimal Subtotal => UnitPrice * Quantity;
            public List<SizeOption> Sizes { get; set; } = new();
        }

        public class SizeOption
        {
            public int PriceId { get; set; }
            public string SizeName { get; set; } = string.Empty;
            public decimal Price { get; set; }
        }

        public async Task OnGet()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // First, remove any cart items that reference deleted prices
            var orphanedItems = await _context.Carts
                .Where(c => c.UserId == userId)
                .Where(c => !_context.MenuPrices.Any(mp => mp.PriceId == c.PriceId))
                .ToListAsync();

            if (orphanedItems.Any())
            {
                _context.Carts.RemoveRange(orphanedItems);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Ignore if rows were already removed concurrently
                }
                TempData["RemovedItemsMessage"] = $"Removed {orphanedItems.Count} unavailable item(s) from your cart.";
            }

            // Merge duplicate cart rows (same user + same priceId)
            var mergedCount = await MergeDuplicateCartItemsAsync(userId);
            if (mergedCount > 0)
            {
                TempData["MergedItemsMessage"] = $"Merged {mergedCount} duplicate cart entries.";
            }

            Items = await _context.Carts
                .Where(c => c.UserId == userId)
                .Include(c => c.MenuPrice)
                    .ThenInclude(mp => mp.MenuItem)
                        .ThenInclude(mi => mi.Category)
                .Include(c => c.MenuPrice)
                    .ThenInclude(mp => mp.Size)
                .OrderByDescending(c => c.AddedAt)
                .Select(c => new CartRowVm
                {
                    CartId = c.CartId,
                    MenuItemId = c.MenuPrice.MenuItemId,
                    CurrentPriceId = c.PriceId,
                    ItemName = c.MenuPrice.MenuItem.ItemName,
                    CategoryName = c.MenuPrice.MenuItem.Category.CategoryName,
                    Image = c.MenuPrice.MenuItem.Image,
                    SizeName = c.MenuPrice.Size.SizeName,
                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice,
                    CurrentPrice = c.MenuPrice.UnitPrice, // Current price from database
                    PriceChanged = c.UnitPrice != c.MenuPrice.UnitPrice // Flag for price changes
                })
                .ToListAsync();

            // Check for price changes and handle based on configuration
            var priceChangedItems = new List<string>();
            var removedItems = new List<string>();
            
            foreach (var item in Items.Where(i => i.PriceChanged).ToList())
            {
                var cartItem = await _context.Carts.FindAsync(item.CartId);
                if (cartItem != null)
                {
                    var oldPrice = cartItem.UnitPrice;
                    var newPrice = item.CurrentPrice;
                    var priceIncrease = (newPrice - oldPrice) / oldPrice;
                    
                    // Handle based on configuration
                    if (_cartSettings.HandlePriceChanges == "Remove" || 
                        (_cartSettings.HandlePriceChanges == "RemoveOnIncrease" && newPrice > oldPrice) ||
                        (_cartSettings.RemoveOnPriceIncrease && priceIncrease > _cartSettings.PriceIncreaseThreshold))
                    {
                        // Remove item from cart
                        _context.Carts.Remove(cartItem);
                        removedItems.Add($"{item.ItemName} ({item.SizeName}) - Price changed from {FormatPrice(oldPrice)} to {FormatPrice(newPrice)}");
                        
                        // Remove from Items list for display
                        Items.Remove(item);
                    }
                    else
                    {
                        // Update price
                        cartItem.UnitPrice = newPrice;
                        item.UnitPrice = newPrice; // Update display model too
                        priceChangedItems.Add($"{item.ItemName} ({item.SizeName}): {FormatPrice(oldPrice)} ? {FormatPrice(newPrice)}");
                    }
                }
            }

            if (priceChangedItems.Any() || removedItems.Any())
            {
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Ignore price-change concurrent modifications
                }
                
                if (priceChangedItems.Any())
                {
                    TempData["PriceUpdateMessage"] = $"Prices have been updated for: {string.Join(", ", priceChangedItems)}";
                }
                
                if (removedItems.Any())
                {
                    TempData["RemovedPriceChangeMessage"] = $"Items removed due to price changes: {string.Join(", ", removedItems)}";
                }
            }

            // Populate available sizes per cart row (unified across all MenuItems with the same ItemName)
            var itemNames = Items.Select(i => i.ItemName).Distinct().ToList();
            var pricesByName = await _context.MenuPrices
                .Include(mp => mp.Size)
                .Include(mp => mp.MenuItem)
                .Where(mp => itemNames.Contains(mp.MenuItem.ItemName)
                             && mp.IsAvailable
                             && mp.MenuItem.IsAvailable)
                .ToListAsync();

            foreach (var row in Items)
            {
                // Unify sizes by picking the lowest-priced entry per size across all menu items sharing the same ItemName
                row.Sizes = pricesByName
                    .Where(p => p.MenuItem.ItemName == row.ItemName)
                    .GroupBy(p => new { p.SizeId, p.Size.SizeName })
                    .Select(g => g.OrderBy(p => p.UnitPrice).First())
                    .OrderBy(p => p.UnitPrice)
                    .Select(p => new SizeOption
                    {
                        PriceId = p.PriceId,
                        SizeName = p.Size.SizeName,
                        Price = p.UnitPrice
                    })
                    .ToList();
            }
        }

        public async Task<IActionResult> OnPostRemoveAsync(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Delete defensively to avoid concurrency exceptions if the row was already removed
            var row = await _context.Carts.FirstOrDefaultAsync(c => c.CartId == id && c.UserId == userId);
            if (row == null)
            {
                return RedirectToPage();
            }

            _context.Carts.Remove(row);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Ignore: the row was already deleted by another operation
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostClearAsync()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var rows = _context.Carts.Where(c => c.UserId == userId);
            _context.Carts.RemoveRange(rows);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Ignore if some rows were already deleted concurrently
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddAsync([FromBody] AddCartDto dto)
        {
            if (!User.Identity!.IsAuthenticated)
            {
                return new JsonResult(new { ok = false, message = "Not authenticated" }) { StatusCode = 401 };
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Validate the price/size exists, is available, and the linked MenuItem is available
            var price = await _context.MenuPrices
                .Include(p => p.MenuItem)
                .FirstOrDefaultAsync(p => p.PriceId == dto.PriceId);
            if (price == null)
            {
                return new JsonResult(new { ok = false, message = "Invalid price/size" }) { StatusCode = 400 };
            }
            if (!price.MenuItem.IsAvailable)
            {
                return new JsonResult(new { ok = false, message = "This product is not available" }) { StatusCode = 400 };
            }
            if (!price.IsAvailable)
            {
                return new JsonResult(new { ok = false, message = "This size is not available" }) { StatusCode = 400 };
            }

            // Use a transaction to prevent race conditions
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Merge any existing duplicates first
                await MergeDuplicateCartItemsAsync(userId);

                // Upsert: if same price_id exists, increase qty
                var existing = await _context.Carts
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.PriceId == dto.PriceId);
                    
                if (existing != null)
                {
                    existing.Quantity += dto.Quantity;
                    existing.UnitPrice = price.UnitPrice; // keep latest unit price
                    existing.AddedAt = DateTime.UtcNow; // Update timestamp
                }
                else
                {
                    var row = new Cart
                    {
                        UserId = userId,
                        PriceId = dto.PriceId,
                        Quantity = dto.Quantity,
                        UnitPrice = price.UnitPrice,
                        AddedAt = DateTime.UtcNow
                    };
                    await _context.Carts.AddAsync(row);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return new JsonResult(new { ok = false, message = "Conflict updating cart. Please try again." }) { StatusCode = 409 };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return new JsonResult(new { ok = true });
        }

        public async Task<IActionResult> OnPostUpdateSizeAsync(int id, int newPriceId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var row = await _context.Carts
                .Include(c => c.MenuPrice)
                    .ThenInclude(mp => mp.MenuItem)
                .FirstOrDefaultAsync(c => c.CartId == id && c.UserId == userId);
            if (row == null) return RedirectToPage();

            var newPrice = await _context.MenuPrices
                .Include(mp => mp.MenuItem)
                .FirstOrDefaultAsync(mp => mp.PriceId == newPriceId);
            if (newPrice == null) return RedirectToPage();

            // Ensure the new size belongs to the same product name
            if (!string.Equals(newPrice.MenuItem.ItemName, row.MenuPrice.MenuItem.ItemName, StringComparison.OrdinalIgnoreCase))
                return RedirectToPage();

            // Prevent selecting a size for an unavailable item or unavailable size
            if (!newPrice.MenuItem.IsAvailable || !newPrice.IsAvailable)
                return RedirectToPage();

            // Check if changing to a size that already exists in cart
            var existingWithNewSize = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserId == userId && c.PriceId == newPriceId && c.CartId != id);

            if (existingWithNewSize != null)
            {
                // Merge: add quantity to existing item and remove current item
                existingWithNewSize.Quantity += row.Quantity;
                _context.Carts.Remove(row);
            }
            else
            {
                // Just update the size/price
                row.PriceId = newPrice.PriceId;
                row.UnitPrice = newPrice.UnitPrice;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Ignore if row was updated/removed concurrently
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateQtyAsync(int id, string op)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var row = await _context.Carts.FirstOrDefaultAsync(c => c.CartId == id && c.UserId == userId);
            if (row == null) return RedirectToPage();

            if (op == "inc") row.Quantity++;
            else if (op == "dec" && row.Quantity > 1) row.Quantity--;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Ignore if row was updated/removed concurrently
            }
            return RedirectToPage();
        }

        public class AddCartDto
        {
            public int PriceId { get; set; }
            public int Quantity { get; set; }
        }
    }
}
