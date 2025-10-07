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

        public async Task OnGetAsync(string status = "all", string search = "", int page = 1)
        {
            StatusFilter = status;
            SearchTerm = search;
            CurrentPage = page;

            // Load available statuses
            AvailableStatuses = await _context.Statuses.OrderBy(s => s.StatusId).ToListAsync();

            // Build base query
            var baseQuery = _context.Orders
                .Include(o => o.User)
                .Include(o => o.Status)
                .Include(o => o.Payment)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuPrice)
                        .ThenInclude(mp => mp.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuPrice)
                        .ThenInclude(mp => mp.Size)
                .AsQueryable();

            // Apply status filter
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                baseQuery = baseQuery.Where(o => o.Status.StatusName.ToLower() == status.ToLower());
            }

            // Apply search filter
            if (!string.IsNullOrEmpty(search))
            {
                baseQuery = baseQuery.Where(o =>
                    o.OrderId.ToString().Contains(search) ||
                    (o.User.FirstName + " " + o.User.LastName).Contains(search) ||
                    (o.User.PhoneNumber != null && o.User.PhoneNumber.Contains(search)) ||
                    (o.DeliveryAddress != null && o.DeliveryAddress.Contains(search)));
            }

            // Calculate statistics
            var allOrders = await _context.Orders.Include(o => o.Status).ToListAsync();
            TotalOrders = allOrders.Count;
            PendingOrders = allOrders.Count(o => o.Status.StatusName == "Pending");
            PreparingOrders = allOrders.Count(o => o.Status.StatusName == "Preparing");
            ReadyOrders = allOrders.Count(o => o.Status.StatusName == "Ready");
            CompletedOrders = allOrders.Count(o => o.Status.StatusName == "Completed");
            TotalRevenue = allOrders.Where(o => o.Status.StatusName == "Completed").Sum(o => o.TotalAmount);
            TodayRevenue = allOrders.Where(o => o.Status.StatusName == "Completed" && TimeUtils.ToPH(o.OrderDate).Date == TimeUtils.NowPH.Date).Sum(o => o.TotalAmount);

            // Get total count for pagination
            var totalCount = await baseQuery.CountAsync();
            TotalPages = (int)Math.Ceiling((double)totalCount / PageSize);

            // Apply pagination and get orders
            var orders = await baseQuery
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // Apply page after materializing to be safe with TimeUtils
            orders = orders
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            // Map to view models
            Orders = orders.Select(o => new AdminOrderViewModel
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
                IsPaid = (o.Payment != null && o.Payment.PaymentName.ToLower() == "gcash")
            }).ToList();
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int orderId, int statusId, long lastUpdateTicks, string? status, string? search, int? page)
        {
            var order = await _context.Orders
                .Include(o => o.Status)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToPage("/Admin/Orders");
            }

            // Concurrency check: prevent multiple updates at once
            if (order.LastUpdate.Ticks != lastUpdateTicks)
            {
                TempData["ErrorMessage"] = "Order status was updated by another action. Please refresh the page.";
                return RedirectToPage("/Admin/Orders", new { status, search, page });
            }

            // Determine allowed next status based on current status
            var currentStatus = order.Status.StatusName;
            var allowedNext = GetNextStatus(currentStatus);

            if (allowedNext == null)
            {
                TempData["ErrorMessage"] = "This order can no longer be updated.";
                return RedirectToPage("/Admin/Orders", new { status, search, page });
            }

            var requestedStatus = await _context.Statuses.FindAsync(statusId);
            if (requestedStatus == null)
            {
                TempData["ErrorMessage"] = "Invalid status.";
                return RedirectToPage("/Admin/Orders", new { status, search, page });
            }

            if (requestedStatus.StatusName != allowedNext)
            {
                TempData["ErrorMessage"] = $"Invalid transition: {currentStatus} ? {requestedStatus.StatusName}. You can only move to {allowedNext}.";
                return RedirectToPage("/Admin/Orders", new { status, search, page });
            }

            // All good: perform update
            var adminUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            order.StatusId = requestedStatus.StatusId;
            order.LastUpdate = DateTime.UtcNow; // stored in UTC, shown in PH via TimeUtils
            order.ModifiedBy = adminUserId;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Order #{orderId} status updated: {currentStatus} ? {requestedStatus.StatusName}.";
            return RedirectToPage("/Admin/Orders", new { status, search, page });
        }

        private static string? GetNextStatus(string current)
        {
            return current switch
            {
                "Pending" => "Preparing",
                "Preparing" => "Ready",
                "Ready" => "Completed",
                _ => null // Completed or Cancelled or unknown
            };
        }

        private static string GetTimeAgo(DateTime orderDate)
        {
            var nowPh = TimeUtils.NowPH;
            var orderPh = TimeUtils.ToPH(orderDate);
            var timeSpan = nowPh - orderPh;
            
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
    }
}