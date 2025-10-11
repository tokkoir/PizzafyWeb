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

        // Purchase Details
        public List<CustomerPurchase> CustomerPurchases { get; set; } = new();

        public class CategoryProductCount
        {
            public string CategoryName { get; set; } = string.Empty;
            public int ProductCount { get; set; }
        }

        public class CustomerPurchase
        {
            public int OrderId { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public DateTime OrderDate { get; set; }
            public decimal TotalAmount { get; set; }
            public string Status { get; set; } = string.Empty;
            public string PaymentMethod { get; set; } = string.Empty;
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

            if (order == null)
            {
                return NotFound();
            }

            var orderDetails = new
            {
                OrderId = order.OrderId,
                CustomerName = $"{order.User.FirstName} {order.User.LastName}",
                CustomerPhone = order.User.PhoneNumber,
                OrderDate = order.OrderDate.ToString("MMM dd, yyyy hh:mm tt"),
                Status = order.Status.StatusName,
                PaymentMethod = order.Payment.PaymentName,
                DeliveryAddress = order.DeliveryAddress,
                DeliveryFee = order.DeliveryFee,
                TotalAmount = order.TotalAmount,
                Items = order.OrderItems.Select(oi => new
                {
                    ItemName = oi.MenuPrice.MenuItem.ItemName,
                    Size = oi.MenuPrice.Size.SizeName,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    Subtotal = oi.Subtotal
                }).ToList()
            };

            return new JsonResult(orderDetails);
        }

        private async Task LoadDashboardData()
        {
            // Sales Overview
            var now = DateTime.Now;
            var startOfDay = now.Date;
            var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            // Selected period sales
            SelectedPeriodSales = await _context.Orders
                .Where(o => o.OrderDate.Date >= StartDate && o.OrderDate.Date <= EndDate)
                .SumAsync(o => o.TotalAmount);

            // 24 hours sales
            Sales24Hours = await _context.Orders
                .Where(o => o.OrderDate >= startOfDay)
                .SumAsync(o => o.TotalAmount);

            // This week's sales
            SalesThisWeek = await _context.Orders
                .Where(o => o.OrderDate >= startOfWeek)
                .SumAsync(o => o.TotalAmount);

            // This month's sales
            SalesThisMonth = await _context.Orders
                .Where(o => o.OrderDate >= startOfMonth)
                .SumAsync(o => o.TotalAmount);

            // Products per category (aggregate by item_name)
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

            // Customer purchases for the selected date range
            CustomerPurchases = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Status)
                .Include(o => o.Payment)
                .Where(o => o.OrderDate.Date >= StartDate && o.OrderDate.Date <= EndDate)
                .OrderByDescending(o => o.OrderDate)
                .Take(50) // Limit to recent 50 orders
                .Select(o => new CustomerPurchase
                {
                    OrderId = o.OrderId,
                    CustomerName = $"{o.User.FirstName} {o.User.LastName}",
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status.StatusName,
                    PaymentMethod = o.Payment.PaymentName
                })
                .ToListAsync();
        }
    }
}