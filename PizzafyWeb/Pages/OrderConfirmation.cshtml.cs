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
    public class OrderConfirmationModel : PageModel
    {
        private readonly PizzafyDbContext _context;

        public OrderConfirmationModel(PizzafyDbContext context)
        {
            _context = context;
        }

        public OrderViewModel? Order { get; set; }

        public class OrderViewModel
        {
            public int OrderId { get; set; }
            public DateTime OrderDate { get; set; }
            public string StatusName { get; set; } = string.Empty;
            public decimal TotalAmount { get; set; }
            public decimal DeliveryFee { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public string PhoneNumber { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public List<OrderItemViewModel> Items { get; set; } = new();
            public decimal Subtotal => Items.Sum(i => i.Subtotal);
        }

        public class OrderItemViewModel
        {
            public string ItemName { get; set; } = string.Empty;
            public string SizeName { get; set; } = string.Empty;
            public string? Image { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Subtotal { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int? orderId)
        {
            if (orderId == null)
            {
                return RedirectToPage("/Index");
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            Order = await _context.Orders
                .Where(o => o.OrderId == orderId && o.UserId == userId)
                .Include(o => o.Status)
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuPrice)
                        .ThenInclude(mp => mp.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuPrice)
                        .ThenInclude(mp => mp.Size)
                .Select(o => new OrderViewModel
                {
                    OrderId = o.OrderId,
                    OrderDate = o.OrderDate,
                    StatusName = o.Status.StatusName,
                    TotalAmount = o.TotalAmount,
                    DeliveryFee = o.DeliveryFee,
                    CustomerName = o.User.FirstName + " " + o.User.LastName,
                    PhoneNumber = o.User.PhoneNumber ?? "",
                    Address = o.DeliveryAddress ?? o.User.Address ?? "",
                    Items = o.OrderItems.Select(oi => new OrderItemViewModel
                    {
                        ItemName = oi.MenuPrice.MenuItem.ItemName,
                        SizeName = oi.MenuPrice.Size.SizeName,
                        Image = oi.MenuPrice.MenuItem.Image,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        Subtotal = oi.Subtotal
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (Order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToPage("/Index");
            }

            return Page();
        }
    }
}