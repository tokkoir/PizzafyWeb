using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;

namespace PizzafyWeb.Pages.Admin
{
    [Authorize(Roles="Admin")]
    public class SalesReportModel : PageModel
    {
        private readonly PizzafyDbContext _context;
        public SalesReportModel(PizzafyDbContext context){ _context = context; }

        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-30);
        public DateTime EndDate { get; set; } = DateTime.Today;
        public decimal TotalSales { get; set; }
        public int OrdersCount { get; set; }
        public decimal AvgOrderValue { get; set; }
        public int CustomersCount { get; set; }
        public List<DailyPoint> DailySales { get; set; } = new();
        public List<TopProductRow> TopProducts { get; set; } = new();
        public List<CustomerSpendRow> CustomerSpends { get; set; } = new();
        public string SelectedRange { get; set; } = string.Empty;

        public class DailyPoint { public string DateLabel { get; set; } = string.Empty; public decimal Amount { get; set; } }
        public class TopProductRow { public string ProductName { get; set; } = string.Empty; public int QtySold { get; set; } public decimal Revenue { get; set; } }
        public class CustomerSpendRow { public string CustomerName { get; set; } = string.Empty; public int Orders { get; set; } public decimal TotalSpent { get; set; } }

        public async Task OnGetAsync(DateTime? start, DateTime? end, string? range)
        {
            // Quick range overrides manual dates
            if (!string.IsNullOrWhiteSpace(range))
            {
                SelectedRange = range.ToLowerInvariant();
                var today = DateTime.Today;
                switch (SelectedRange)
                {
                    case "today":
                        StartDate = today;
                        EndDate = today;
                        break;
                    case "week":
                        // ISO week start Monday
                        int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                        StartDate = today.AddDays(-diff);
                        EndDate = today;
                        break;
                    case "month":
                        StartDate = new DateTime(today.Year, today.Month, 1);
                        EndDate = today;
                        break;
                    default:
                        SelectedRange = string.Empty;
                        break;
                }
            }
            else
            {
                if (start.HasValue) StartDate = start.Value.Date;
                if (end.HasValue) EndDate = end.Value.Date;
            }

            if (EndDate < StartDate) EndDate = StartDate;

            var orders = await _context.Orders
                .Include(o => o.User)
                .Where(o => o.OrderDate.Date >= StartDate && o.OrderDate.Date <= EndDate)
                .ToListAsync();

            TotalSales = orders.Sum(o => o.TotalAmount);
            OrdersCount = orders.Count;
            AvgOrderValue = OrdersCount > 0 ? orders.Average(o => o.TotalAmount) : 0m;
            CustomersCount = orders.Select(o => o.UserId).Distinct().Count();

            DailySales = orders
                .GroupBy(o => o.OrderDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new DailyPoint { DateLabel = g.Key.ToString("MMM dd"), Amount = g.Sum(x => x.TotalAmount) })
                .ToList();

            TopProducts = await _context.OrderItems
                .Include(oi => oi.MenuPrice).ThenInclude(mp => mp.MenuItem)
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.OrderDate.Date >= StartDate && oi.Order.OrderDate.Date <= EndDate)
                .GroupBy(oi => oi.MenuPrice.MenuItem.ItemName)
                .Select(g => new TopProductRow { ProductName = g.Key, QtySold = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.Subtotal) })
                .OrderByDescending(r => r.Revenue)
                .Take(5)
                .ToListAsync();

            CustomerSpends = orders
                .GroupBy(o => new { o.UserId, o.User.FirstName, o.User.LastName })
                .Select(g => new CustomerSpendRow { CustomerName = g.Key.FirstName + " " + g.Key.LastName, Orders = g.Count(), TotalSpent = g.Sum(x => x.TotalAmount) })
                .OrderByDescending(r => r.TotalSpent)
                .Take(5)
                .ToList();
        }
    }
}
