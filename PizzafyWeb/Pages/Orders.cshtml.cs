using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using PizzafyWeb.Utils;
using System.Security.Claims;
using System.Text;

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

        public class OrderDetailViewModel
        {
            public int OrderId { get; set; }
            public DateTime OrderDate { get; set; }
            public string StatusName { get; set; } = string.Empty;
            public decimal TotalAmount { get; set; }
            public decimal DeliveryFee { get; set; }
            public string CustomerName { get; set; } = string.Empty;
            public string PhoneNumber { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public string PaymentMethod { get; set; } = string.Empty;
            public List<OrderItemDetail> Items { get; set; } = new();
            public decimal Subtotal => Items.Sum(i => i.Subtotal);
        }

        public class OrderItemDetail
        {
            public string ItemName { get; set; } = string.Empty;
            public string SizeName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Subtotal { get; set; }
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

        public async Task<IActionResult> OnGetDownloadReceiptAsync(int orderId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var order = await _context.Orders
                .Where(o => o.OrderId == orderId && o.UserId == userId)
                .Include(o => o.Status)
                .Include(o => o.User)
                .Include(o => o.Payment)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuPrice)
                        .ThenInclude(mp => mp.MenuItem)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuPrice)
                        .ThenInclude(mp => mp.Size)
                .Select(o => new OrderDetailViewModel
                {
                    OrderId = o.OrderId,
                    OrderDate = o.OrderDate,
                    StatusName = o.Status.StatusName,
                    TotalAmount = o.TotalAmount,
                    DeliveryFee = o.DeliveryFee,
                    CustomerName = o.User.FirstName + " " + o.User.LastName,
                    PhoneNumber = o.User.PhoneNumber ?? "",
                    Address = o.DeliveryAddress ?? o.User.Address ?? "",
                    PaymentMethod = o.Payment.PaymentName,
                    Items = o.OrderItems.Select(oi => new OrderItemDetail
                    {
                        ItemName = oi.MenuPrice.MenuItem.ItemName,
                        SizeName = oi.MenuPrice.Size.SizeName,
                        Quantity = oi.Quantity,
                        UnitPrice = oi.UnitPrice,
                        Subtotal = oi.Subtotal
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound();
            }

            // Generate HTML receipt
            var receiptHtml = GenerateReceiptHtml(order);
            var bytes = Encoding.UTF8.GetBytes(receiptHtml);

            // Ensure UTF-8 charset for proper symbols rendering
            return File(bytes, "text/html; charset=utf-8", $"Pizzafy_Receipt_Order_{orderId}.html");
        }

        private static string GetTimeAgo(DateTime orderDate)
        {
            // Convert to PH time for relative calculation
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

        private string GenerateReceiptHtml(OrderDetailViewModel order)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='utf-8'>");
            html.AppendLine("<meta http-equiv='Content-Type' content='text/html; charset=utf-8'>");
            html.AppendLine("<title>Pizzafy Receipt - Order #" + order.OrderId + "</title>");
            html.AppendLine("<style>");
            html.AppendLine(@"
                body { 
                    font-family: 'Arial', sans-serif; 
                    max-width: 600px; 
                    margin: 0 auto; 
                    padding: 20px; 
                    background: white;
                    color: #333;
                }
                .receipt-header { 
                    text-align: center; 
                    border-bottom: 2px solid #8c1007; 
                    padding-bottom: 20px; 
                    margin-bottom: 30px; 
                }
                .logo { 
                    font-size: 2.5em; 
                    font-weight: bold; 
                    color: #8c1007; 
                    margin-bottom: 10px; 
                    letter-spacing: 2px;
                }
                .receipt-title { 
                    font-size: 1.5em; 
                    margin: 20px 0; 
                    color: #333; 
                }
                .order-info { 
                    background: #f8f9fa; 
                    padding: 20px; 
                    border-radius: 8px; 
                    margin-bottom: 20px; 
                }
                .info-row { 
                    display: flex; 
                    justify-content: space-between; 
                    margin-bottom: 10px; 
                }
                .info-label { 
                    font-weight: bold; 
                    color: #666; 
                }
                .items-section { 
                    margin: 30px 0; 
                }
                .item { 
                    display: flex; 
                    justify-content: space-between; 
                    align-items: center; 
                    padding: 15px 0; 
                    border-bottom: 1px solid #eee; 
                }
                .item-details { 
                    flex: 1; 
                }
                .item-name { 
                    font-weight: bold; 
                    margin-bottom: 5px; 
                }
                .item-size { 
                    color: #666; 
                    font-size: 0.9em; 
                }
                .item-quantity { 
                    color: #666; 
                    margin-top: 5px; 
                }
                .item-price { 
                    font-weight: bold; 
                    color: #8c1007; 
                }
                .totals { 
                    background: #f8f9fa; 
                    padding: 20px; 
                    border-radius: 8px; 
                    margin-top: 20px; 
                }
                .total-row { 
                    display: flex; 
                    justify-content: space-between; 
                    margin-bottom: 10px; 
                }
                .grand-total { 
                    font-size: 1.3em; 
                    font-weight: bold; 
                    color: #8c1007; 
                    border-top: 2px solid #8c1007; 
                    padding-top: 15px; 
                    margin-top: 15px; 
                }
                .receipt-footer { 
                    text-align: center; 
                    margin-top: 40px; 
                    padding-top: 20px; 
                    border-top: 1px solid #eee; 
                    color: #666; 
                }
                .thank-you { 
                    font-size: 1.2em; 
                    color: #8c1007; 
                    margin-bottom: 10px; 
                }
                @media print {
                    body { margin: 0; padding: 15px; }
                    .no-print { display: none; }
                }
            ");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            // Header
            html.AppendLine("<div class='receipt-header'>");
            html.AppendLine("<div class='logo'>PIZZAFY</div>");
            html.AppendLine("<div>Delicious Pizza Delivered Fresh</div>");
            html.AppendLine("<div class='receipt-title'>ORDER RECEIPT</div>");
            html.AppendLine("</div>");

            // Order Information
            html.AppendLine("<div class='order-info'>");
            html.AppendLine($"<div class='info-row'><span class='info-label'>Order Number:</span><span>#{order.OrderId}</span></div>");
            html.AppendLine($"<div class='info-row'><span class='info-label'>Order Date:</span><span>{TimeUtils.FormatPH(order.OrderDate, "MMMM dd, yyyy 'at' hh:mm tt")}</span></div>");
            html.AppendLine($"<div class='info-row'><span class='info-label'>Status:</span><span>{order.StatusName}</span></div>");
            html.AppendLine($"<div class='info-row'><span class='info-label'>Payment Method:</span><span>{order.PaymentMethod}</span></div>");
            html.AppendLine("</div>");

            // Customer Information
            html.AppendLine("<div class='order-info'>");
            html.AppendLine("<h3 style='margin-top: 0; color: #8c1007;'>Customer Information</h3>");
            html.AppendLine($"<div class='info-row'><span class='info-label'>Name:</span><span>{order.CustomerName}</span></div>");
            html.AppendLine($"<div class='info-row'><span class='info-label'>Phone:</span><span>{order.PhoneNumber}</span></div>");
            html.AppendLine($"<div class='info-row'><span class='info-label'>Delivery Address:</span><span>{order.Address}</span></div>");
            html.AppendLine("</div>");

            // Items
            html.AppendLine("<div class='items-section'>");
            html.AppendLine("<h3 style='color: #8c1007; margin-bottom: 20px;'>Order Items</h3>");
            
            foreach (var item in order.Items)
            {
                html.AppendLine("<div class='item'>");
                html.AppendLine("<div class='item-details'>");
                html.AppendLine($"<div class='item-name'>{item.ItemName}</div>");
                html.AppendLine($"<div class='item-size'>{item.SizeName}</div>");
                html.AppendLine($"<div class='item-quantity'>{item.Quantity} &times; &#8369;{item.UnitPrice:0.00}</div>");
                html.AppendLine("</div>");
                html.AppendLine($"<div class='item-price'>&#8369;{item.Subtotal:0.00}</div>");
                html.AppendLine("</div>");
            }
            html.AppendLine("</div>");

            // Totals
            html.AppendLine("<div class='totals'>");
            html.AppendLine($"<div class='total-row'><span>Subtotal:</span><span>&#8369;{order.Subtotal:0.00}</span></div>");
            
            if (order.DeliveryFee == 0)
            {
                html.AppendLine("<div class='total-row'><span>Delivery Fee:</span><span style='color: #28a745;'>FREE</span></div>");
            }
            else
            {
                html.AppendLine($"<div class='total-row'><span>Delivery Fee:</span><span>&#8369;{order.DeliveryFee:0.00}</span></div>");
            }
            
            html.AppendLine($"<div class='total-row grand-total'><span>Total Amount:</span><span>&#8369;{order.TotalAmount:0.00}</span></div>");
            html.AppendLine("</div>");

            // Footer
            html.AppendLine("<div class='receipt-footer'>");
            html.AppendLine("<div class='thank-you'>Thank you for choosing Pizzafy!</div>");
            html.AppendLine("<div>We hope you enjoy your delicious meal.</div>");
            html.AppendLine("<div style='margin-top: 20px; font-size: 0.9em;'>");
            html.AppendLine("For any questions or concerns, please contact us.<br>");
            html.AppendLine("Visit us again at <strong>pizzafy.com</strong>");
            html.AppendLine("</div>");
            html.AppendLine("</div>");

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
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