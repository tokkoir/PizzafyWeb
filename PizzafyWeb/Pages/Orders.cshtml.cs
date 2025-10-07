using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using System.Security.Claims;

namespace PizzafyWeb.Pages
{
    [Authorize]
    public class OrdersModel : PageModel
    {
        private readonly PizzafyDbContext _context;

        public OrdersModel(PizzafyDbContext context)
        {
            _context = context;
        }

        public List<OrderSummaryViewModel> Orders { get; set; } = new();
        public string StatusFilter { get; set; } = "all";
        public int TotalOrders => Orders.Count;
        public decimal TotalSpent => Orders.Sum(o => o.TotalAmount);
        
        // Order counts by status
        public int AllOrdersCount { get; set; }
        public int PendingCount { get; set; }
        public int PreparingCount { get; set; }
        public int ReadyCount { get; set; }
        public int CompletedCount { get; set; }

        public class OrderSummaryViewModel
        {
            public int OrderId { get; set; }
            public DateTime OrderDate { get; set; }
            public string StatusName { get; set; } = string.Empty;
            public decimal TotalAmount { get; set; }
            public decimal DeliveryFee { get; set; }
            public string DeliveryAddress { get; set; } = string.Empty;
            public int ItemCount { get; set; }
            public List<string> ItemNames { get; set; } = new();
            public string TimeAgo { get; set; } = string.Empty;
        }

        public async Task OnGetAsync(string status = "all")
        {
            StatusFilter = status;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Get all orders for counting
            var allUserOrders = await _context.Orders
                .Where(o => o.UserId == userId)
                .Include(o => o.Status)
                .ToListAsync();

            // Calculate counts for each status - using exact database status names
            AllOrdersCount = allUserOrders.Count;
            PendingCount = allUserOrders.Count(o => o.Status.StatusName.ToLower() == "pending");
            PreparingCount = allUserOrders.Count(o => o.Status.StatusName.ToLower() == "preparing");
            ReadyCount = allUserOrders.Count(o => o.Status.StatusName.ToLower() == "ready");
            CompletedCount = allUserOrders.Count(o => o.Status.StatusName.ToLower() == "completed");

            // Build query for filtered orders
            IQueryable<Order> baseQuery = _context.Orders
                .Where(o => o.UserId == userId)
                .Include(o => o.Status)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuPrice)
                        .ThenInclude(mp => mp.MenuItem);

            // Apply status filter - using exact database status names
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                baseQuery = baseQuery.Where(o => o.Status.StatusName.ToLower() == status.ToLower());
            }

            var orders = await baseQuery
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            Orders = orders.Select(o => new OrderSummaryViewModel
            {
                OrderId = o.OrderId,
                OrderDate = o.OrderDate,
                StatusName = o.Status.StatusName,
                TotalAmount = o.TotalAmount,
                DeliveryFee = o.DeliveryFee,
                DeliveryAddress = o.DeliveryAddress ?? "N/A",
                ItemCount = o.OrderItems.Sum(oi => oi.Quantity),
                ItemNames = o.OrderItems.Select(oi => oi.MenuPrice.MenuItem.ItemName).Distinct().ToList(),
                TimeAgo = GetTimeAgo(o.OrderDate)
            }).ToList();
        }

        private static string GetTimeAgo(DateTime orderDate)
        {
            var timeSpan = DateTime.UtcNow - orderDate;
            
            if (timeSpan.TotalDays >= 365)
                return $"{(int)(timeSpan.TotalDays / 365)} year{((int)(timeSpan.TotalDays / 365) != 1 ? "s" : "")} ago";
            if (timeSpan.TotalDays >= 30)
                return $"{(int)(timeSpan.TotalDays / 30)} month{((int)(timeSpan.TotalDays / 30) != 1 ? "s" : "")} ago";
            if (timeSpan.TotalDays >= 1)
                return $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays != 1 ? "s" : "")} ago";
            if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours != 1 ? "s" : "")} ago";
            if (timeSpan.TotalMinutes >= 1)
                return $"{(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes != 1 ? "s" : "")} ago";
            
            return "Just now";
        }

        public async Task<IActionResult> OnPostReorderAsync(int orderId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            var order = await _context.Orders
                .Where(o => o.OrderId == orderId && o.UserId == userId)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuPrice)
                .FirstOrDefaultAsync();

            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToPage();
            }

            try
            {
                // Add items back to cart
                foreach (var item in order.OrderItems)
                {
                    // Check if the price/size still exists
                    var currentPrice = await _context.MenuPrices
                        .FirstOrDefaultAsync(mp => mp.PriceId == item.PriceId);
                    
                    if (currentPrice != null)
                    {
                        var existingCartItem = await _context.Carts
                            .FirstOrDefaultAsync(c => c.UserId == userId && c.PriceId == item.PriceId);

                        if (existingCartItem != null)
                        {
                            existingCartItem.Quantity += item.Quantity;
                            existingCartItem.UnitPrice = currentPrice.UnitPrice; // Use current price
                        }
                        else
                        {
                            var cartItem = new Cart
                            {
                                UserId = userId,
                                PriceId = item.PriceId,
                                Quantity = item.Quantity,
                                UnitPrice = currentPrice.UnitPrice, // Use current price
                                AddedAt = DateTime.UtcNow
                            };
                            _context.Carts.Add(cartItem);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Items from your previous order have been added to your cart!";
                return RedirectToPage("/Cart");
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Unable to reorder. Please try again.";
                return RedirectToPage();
            }
        }
    }
}