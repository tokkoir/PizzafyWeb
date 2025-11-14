using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using PizzafyWeb.Utils;
using System.Security.Claims;

namespace PizzafyWeb.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public class OrdersModel : PageModel
    {
        private readonly PizzafyDbContext _context;

        public OrdersModel(PizzafyDbContext context)
        {
            _context = context;
        }

        public List<AdminOrderViewModel> Orders { get; set; } = new();
        public List<Status> AvailableStatuses { get; set; } = new();
        public string StatusFilter { get; set; } = "all";
        public string SearchTerm { get; set; } = "";
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 10;

        // Order statistics
        public int TotalOrders { get; set; }
        public int PendingOrders { get; set; }
        public int PreparingOrders { get; set; }
        public int ReadyOrders { get; set; }
        public int CompletedOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TodayRevenue { get; set; }

        public class AdminOrderViewModel
        {
            public int OrderId { get; set; }
            public DateTime OrderDate { get; set; }
            public DateTime LastUpdate { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public string CustomerPhone { get; set; } = string.Empty;
            public string DeliveryAddress { get; set; } = string.Empty;
            public string StatusName { get; set; } = string.Empty;
            public int StatusId { get; set; }
            public decimal TotalAmount { get; set; }
            public decimal DeliveryFee { get; set; }
            public int ItemCount { get; set; }
            public List<OrderItemInfo> Items { get; set; } = new();
            public string TimeAgo { get; set; } = string.Empty;
            public string PaymentMethod { get; set; } = string.Empty;
            public bool IsPaid { get; set; }
        }

        public class OrderItemInfo
        {
            public string ItemName { get; set; } = string.Empty;
            public string SizeName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Subtotal { get; set; }
        }

        // Accept 'pg' instead of 'page' to avoid Razor Pages route conflicts
        public async Task OnGetAsync(string status = "all", string search = "", int pg = 1)
        {
            StatusFilter = status;
            SearchTerm = search;

            // Load available statuses
            AvailableStatuses = await _context.Statuses.OrderBy(s => s.StatusId).ToListAsync();

            // Base query (no includes yet for pagination reliability)
            var baseQuery = _context.Orders
                .AsNoTracking()
                .Include(o => o.User)
                .Include(o => o.Status)
                .Include(o => o.Payment)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                var sLower = status.ToLower();
                baseQuery = baseQuery.Where(o => o.Status.StatusName.ToLower() == sLower);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                baseQuery = baseQuery.Where(o => (o.User.FirstName + " " + o.User.LastName).ToLower().Contains(term) || o.User.FirstName.ToLower().Contains(term) || o.User.LastName.ToLower().Contains(term));
            }

            var totalCount = await baseQuery.CountAsync();
            TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
            if (TotalPages < 1) TotalPages = 1;
            CurrentPage = Math.Clamp(pg, 1, TotalPages);

            var skip = (CurrentPage - 1) * PageSize;

            // Page IDs first
            var pageIds = await baseQuery
                .OrderByDescending(o => o.OrderDate)
                .Select(o => o.OrderId)
                .Skip(skip)
                .Take(PageSize)
                .ToListAsync();

            // Load full orders for page IDs
            var pageOrders = await _context.Orders
                .Where(o => pageIds.Contains(o.OrderId))
                .Include(o => o.User)
                .Include(o => o.Status)
                .Include(o => o.Payment)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuPrice)
                        .ThenInclude(mp => mp.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuPrice)
                        .ThenInclude(mp => mp.Size)
                .OrderByDescending(o => o.OrderDate)
                .AsNoTracking()
                .ToListAsync();

            // Stats (independent of filters/paging)
            var allOrders = await _context.Orders.AsNoTracking().Include(o => o.Status).ToListAsync();
            TotalOrders = allOrders.Count;
            PendingOrders = allOrders.Count(o => o.Status.StatusName == "Pending");
            PreparingOrders = allOrders.Count(o => o.Status.StatusName == "Preparing");
            ReadyOrders = allOrders.Count(o => o.Status.StatusName == "Ready");
            CompletedOrders = allOrders.Count(o => o.Status.StatusName == "Completed");
            TotalRevenue = allOrders.Where(o => o.Status.StatusName == "Completed").Sum(o => o.TotalAmount);
            TodayRevenue = allOrders.Where(o => o.Status.StatusName == "Completed" && TimeUtils.ToPH(o.OrderDate).Date == TimeUtils.NowPH.Date).Sum(o => o.TotalAmount);

            Orders = pageOrders.Select(o => Map(o)).ToList();
        }

        private AdminOrderViewModel Map(Order o)
        {
            var payName = o.Payment?.PaymentName?.ToLower() ?? string.Empty;
            var isGcash = payName == "gcash";
            var isCod = payName.Contains("cash");
            var isCompleted = o.Status.StatusName == "Completed";
            return new AdminOrderViewModel
            {
                OrderId = o.OrderId,
                OrderDate = o.OrderDate,
                LastUpdate = o.LastUpdate,
                CustomerName = $"{o.User.FirstName} {o.User.LastName}".Trim(),
                CustomerPhone = o.User.PhoneNumber ?? "N/A",
                DeliveryAddress = o.DeliveryAddress ?? o.User.Address ?? "N/A",
                StatusName = o.Status.StatusName,
                StatusId = o.StatusId,
                TotalAmount = o.TotalAmount,
                DeliveryFee = o.DeliveryFee,
                ItemCount = o.OrderItems.Sum(oi => oi.Quantity),
                Items = o.OrderItems.Select(oi => new OrderItemInfo
                {
                    ItemName = oi.MenuPrice.MenuItem.ItemName,
                    SizeName = oi.MenuPrice.Size.SizeName,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    Subtotal = oi.Subtotal
                }).ToList(),
                TimeAgo = GetTimeAgo(o.OrderDate),
                PaymentMethod = o.Payment?.PaymentName ?? "N/A",
                IsPaid = isGcash || (isCod && isCompleted)
            };
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int orderId, int statusId, long lastUpdateTicks, string? status, string? search, int? pg)
        {
            var order = await _context.Orders
                .Include(o => o.Status)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToPage("/Admin/Orders");
            }

            if (order.LastUpdate.Ticks != lastUpdateTicks)
            {
                TempData["ErrorMessage"] = "Order status was updated by another action. Please refresh the page.";
                return RedirectToPage("/Admin/Orders", new { status, search, pg });
            }

            var currentStatus = order.Status.StatusName;
            var allowedNext = GetNextStatus(currentStatus);
            if (allowedNext == null)
            {
                TempData["ErrorMessage"] = "This order can no longer be updated.";
                return RedirectToPage("/Admin/Orders", new { status, search, pg });
            }

            var requestedStatus = await _context.Statuses.FindAsync(statusId);
            if (requestedStatus == null)
            {
                TempData["ErrorMessage"] = "Invalid status.";
                return RedirectToPage("/Admin/Orders", new { status, search, pg });
            }

            if (requestedStatus.StatusName != allowedNext)
            {
                TempData["ErrorMessage"] = $"Invalid transition: {currentStatus} ? {requestedStatus.StatusName}. You can only move to {allowedNext}.";
                return RedirectToPage("/Admin/Orders", new { status, search, pg });
            }

            var adminUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            order.StatusId = requestedStatus.StatusId;
            order.LastUpdate = DateTime.UtcNow;
            order.ModifiedBy = adminUserId;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Order #{orderId} status updated: {currentStatus} ? {requestedStatus.StatusName}.";
            return RedirectToPage("/Admin/Orders", new { status, search, pg });
        }

        private static string? GetNextStatus(string current) => current switch
        {
            "Pending" => "Preparing",
            "Preparing" => "Ready",
            "Ready" => "Completed",
            _ => null
        };

        private static string GetTimeAgo(DateTime orderDate)
        {
            var nowPh = TimeUtils.NowPH;
            var orderPh = TimeUtils.ToPH(orderDate);
            var timeSpan = nowPh - orderPh;
            if (timeSpan.TotalDays >= 365) return $"{(int)(timeSpan.TotalDays / 365)} year{((int)(timeSpan.TotalDays / 365) != 1 ? "s" : "")} ago";
            if (timeSpan.TotalDays >= 30) return $"{(int)(timeSpan.TotalDays / 30)} month{((int)(timeSpan.TotalDays / 30) != 1 ? "s" : "")} ago";
            if (timeSpan.TotalDays >= 1) return $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays != 1 ? "s" : "")} ago";
            if (timeSpan.TotalHours >= 1) return $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours != 1 ? "s" : "")} ago";
            if (timeSpan.TotalMinutes >= 1) return $"{(int)timeSpan.TotalMinutes} minute{((int)timeSpan.TotalMinutes != 1 ? "s" : "")} ago";
            return "Just now";
        }
    }
}