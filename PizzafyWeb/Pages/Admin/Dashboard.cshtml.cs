using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using System.ComponentModel.DataAnnotations;

namespace PizzafyWeb.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class DashboardModel : PageModel
    {
        private readonly PizzafyDbContext _context;

        public DashboardModel(PizzafyDbContext context)
        {
            _context = context;
        }

        [BindProperty, DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-30);
        [BindProperty, DataType(DataType.Date)]
        public DateTime EndDate { get; set; } = DateTime.Today;

        // Sales Overview
        public decimal SelectedPeriodSales { get; set; }
        public decimal Sales24Hours { get; set; }
        public decimal SalesThisWeek { get; set; }
        public decimal SalesThisMonth { get; set; }

        // Products per Category
        public List<CategoryProductCount> ProductsByCategory { get; set; } = new();

        // Purchase Details (sample recent orders per customer)
        public List<CustomerPurchase> CustomerPurchases { get; set; } = new();

        // Top aggregates
        public List<TopProduct> TopProducts { get; set; } = new();
        public List<TopCustomer> TopCustomers { get; set; } = new();

        public class CategoryProductCount
        {
            public string CategoryName { get; set; } = string.Empty;
            public int ProductCount { get; set; }
        }

        public class CustomerPurchase
        {
            public int OrderId { get; set; }
            public int UserId { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public DateTime OrderDate { get; set; }
            public decimal TotalAmount { get; set; }
            public string Status { get; set; } = string.Empty;
            public string PaymentMethod { get; set; } = string.Empty;
        }

        public class TopProduct
        {
            public string ProductName { get; set; } = string.Empty;
            public int PurchasesCount { get; set; }
            public int TotalQuantity { get; set; }
        }

        public class TopCustomer
        {
            public string CustomerName { get; set; } = string.Empty;
            public int PurchasesCount { get; set; }
            public decimal TotalAmount { get; set; }
        }

        public async Task OnGetAsync()
        {
            await LoadDashboardData();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (EndDate < StartDate)
            {
                ModelState.AddModelError("EndDate", "End date must be after start date");
                EndDate = StartDate.AddDays(1);
            }
            await LoadDashboardData();
            return Page();
        }

        // Order details (single order)
        public async Task<IActionResult> OnGetOrderDetailsAsync(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Status)
                .Include(o => o.Payment)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuPrice)
                        .ThenInclude(mp => mp.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuPrice)
                        .ThenInclude(mp => mp.Size)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null) return NotFound();

            var result = new
            {
                orderId = order.OrderId,
                customerName = $"{order.User.FirstName} {order.User.LastName}".Trim(),
                customerPhone = order.User.PhoneNumber,
                orderDate = order.OrderDate.ToString("MMM dd, yyyy hh:mm tt"),
                status = order.Status.StatusName,
                paymentMethod = order.Payment?.PaymentName ?? "N/A",
                deliveryAddress = order.DeliveryAddress,
                deliveryFee = order.DeliveryFee,
                totalAmount = order.TotalAmount,
                items = order.OrderItems.Select(oi => new
                {
                    itemName = oi.MenuPrice.MenuItem.ItemName,
                    size = oi.MenuPrice.Size.SizeName,
                    quantity = oi.Quantity,
                    unitPrice = oi.UnitPrice,
                    subtotal = oi.Subtotal
                }).ToList()
            };
            return new JsonResult(result);
        }

        // Customer orders list (for modal 1)
        public async Task<IActionResult> OnGetCustomerOrdersAsync(int userId)
        {
            var exists = await _context.Users.AnyAsync(u => u.UserId == userId);
            if (!exists) return NotFound(new { message = "User not found" });

            var orders = await _context.Orders
                .Include(o => o.Status)
                .Include(o => o.Payment)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Take(200) // safety cap
                .Select(o => new
                {
                    orderId = o.OrderId,
                    orderDate = o.OrderDate.ToString("yyyy-MM-dd HH:mm"),
                    totalAmount = o.TotalAmount,
                    status = o.Status.StatusName,
                    paymentMethod = o.Payment.PaymentName
                })
                .ToListAsync();

            return new JsonResult(new { userId, orders });
        }

        private async Task LoadDashboardData()
        {
            var now = DateTime.Now;
            var startOfDay = now.Date;
            var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            SelectedPeriodSales = await _context.Orders
                .Where(o => o.OrderDate.Date >= StartDate && o.OrderDate.Date <= EndDate)
                .SumAsync(o => o.TotalAmount);

            Sales24Hours = await _context.Orders.Where(o => o.OrderDate >= startOfDay).SumAsync(o => o.TotalAmount);
            SalesThisWeek = await _context.Orders.Where(o => o.OrderDate >= startOfWeek).SumAsync(o => o.TotalAmount);
            SalesThisMonth = await _context.Orders.Where(o => o.OrderDate >= startOfMonth).SumAsync(o => o.TotalAmount);

            ProductsByCategory = await _context.MenuItems
                .Include(mi => mi.Category)
                .GroupBy(mi => mi.Category.CategoryName)
                .Select(g => new CategoryProductCount
                {
                    CategoryName = g.Key,
                    ProductCount = g.Select(mi => mi.ItemName).Distinct().Count()
                })
                .OrderBy(c => c.CategoryName)
                .ToListAsync();

            CustomerPurchases = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Status)
                .Include(o => o.Payment)
                .Where(o => o.OrderDate.Date >= StartDate && o.OrderDate.Date <= EndDate)
                .OrderByDescending(o => o.OrderDate)
                .Take(200)
                .Select(o => new CustomerPurchase
                {
                    OrderId = o.OrderId,
                    UserId = o.UserId,
                    CustomerName = $"{o.User.FirstName} {o.User.LastName}".Trim(),
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status.StatusName,
                    PaymentMethod = o.Payment.PaymentName
                })
                .ToListAsync();

            TopProducts = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.MenuPrice).ThenInclude(mp => mp.MenuItem)
                .Where(oi => oi.Order.OrderDate.Date >= StartDate && oi.Order.OrderDate.Date <= EndDate)
                .GroupBy(oi => oi.MenuPrice.MenuItem.ItemName)
                .Select(g => new TopProduct
                {
                    ProductName = g.Key,
                    PurchasesCount = g.Select(x => x.OrderId).Distinct().Count(),
                    TotalQuantity = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(tp => tp.PurchasesCount)
                .ThenByDescending(tp => tp.TotalQuantity)
                .Take(3)
                .ToListAsync();

            TopCustomers = await _context.Orders
                .Include(o => o.User)
                .Where(o => o.OrderDate.Date >= StartDate && o.OrderDate.Date <= EndDate)
                .GroupBy(o => new { o.UserId, o.User.FirstName, o.User.LastName })
                .Select(g => new TopCustomer
                {
                    CustomerName = g.Key.FirstName + " " + g.Key.LastName,
                    PurchasesCount = g.Count(),
                    TotalAmount = g.Sum(x => x.TotalAmount)
                })
                .OrderByDescending(tc => tc.PurchasesCount)
                .ThenByDescending(tc => tc.TotalAmount)
                .Take(3)
                .ToListAsync();
        }
    }
}