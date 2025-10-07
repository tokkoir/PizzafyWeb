using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using PizzafyWeb.Utils;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace PizzafyWeb.Pages
{
    [Authorize]
    public class CheckoutModel : PageModel
    {
        private readonly PizzafyDbContext _context;
        private readonly IConfiguration _configuration;

        public CheckoutModel(PizzafyDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public List<CartItemViewModel> CartItems { get; set; } = new();
        public List<Payment> PaymentMethods { get; set; } = new();
        [BindProperty]
        [Required(ErrorMessage = "Please choose a payment method")]
        public int SelectedPaymentId { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "GCash number is required")]
        [RegularExpression(@"^(09\d{9}|\+639\d{9})$", ErrorMessage = "Please enter a valid 11-digit GCash number")]
        public string? GCashNumber { get; set; }

        public decimal Subtotal => CartItems.Sum(i => i.Subtotal);
        public decimal DeliveryFee { get; set; } = 19m; // Default delivery fee
        public decimal Total => Subtotal + DeliveryFee;
        public string GoogleMapsApiKey => _configuration["GoogleMaps:ApiKey"] ?? "";
        public string CustomerFullName { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Delivery address is required")]
        [MaxLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
        public string DeliveryAddress { get; set; } = string.Empty;

        [BindProperty]
        [MaxLength(200, ErrorMessage = "Special instructions cannot exceed 200 characters")]
        public string? SpecialInstructions { get; set; }

        [BindProperty]
        public decimal CalculatedDeliveryFee { get; set; } = 19m;

        public class CartItemViewModel
        {
            public int CartId { get; set; }
            public int PriceId { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public string SizeName { get; set; } = string.Empty;
            public string? Image { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Subtotal => UnitPrice * Quantity;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return RedirectToPage("/Login");
            }

            PaymentMethods = await _context.Payments.OrderBy(p => p.PaymentId).ToListAsync();
            if (PaymentMethods.Any()) SelectedPaymentId = PaymentMethods.First().PaymentId;

            // Load cart items
            CartItems = await _context.Carts
                .Where(c => c.UserId == userId)
                .Include(c => c.MenuPrice)
                    .ThenInclude(mp => mp.MenuItem)
                .Include(c => c.MenuPrice)
                    .ThenInclude(mp => mp.Size)
                .OrderBy(c => c.AddedAt)
                .Select(c => new CartItemViewModel
                {
                    CartId = c.CartId,
                    PriceId = c.PriceId,
                    ItemName = c.MenuPrice.MenuItem.ItemName,
                    SizeName = c.MenuPrice.Size.SizeName,
                    Image = c.MenuPrice.MenuItem.Image,
                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice
                })
                .ToListAsync();

            // Redirect to cart if empty
            if (!CartItems.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty. Please add items before checking out.";
                return RedirectToPage("/Cart");
            }

            // Pre-fill user information - fetch from database but allow editing phone only
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                PhoneNumber = user.PhoneNumber ?? "";
                DeliveryAddress = user.Address ?? "";
                CustomerFullName = $"{user.FirstName} {user.LastName}".Trim();
                
                // Calculate delivery fee if address exists
                if (!string.IsNullOrEmpty(DeliveryAddress))
                {
                    DeliveryFee = await CalculateDeliveryFeeAsync(DeliveryAddress);
                    CalculatedDeliveryFee = DeliveryFee;
                }
            }

            return Page();
        }

        /// <summary>
        /// Calculate delivery fee based on distance from restaurant
        /// </summary>
        private async Task<decimal> CalculateDeliveryFeeAsync(string deliveryAddress)
        {
            try
            {
                const string restaurantAddress = "123 Pizza Street, Metro Manila, Philippines"; // Configure this
                const decimal baseFee = 19m;
                const decimal feePerKm = 5m;
                
                // If Google Maps API key is not configured, return default fee
                if (string.IsNullOrEmpty(GoogleMapsApiKey))
                {
                    return baseFee;
                }

                // Call Google Distance Matrix API
                using var httpClient = new HttpClient();
                var encodedOrigin = Uri.EscapeDataString(restaurantAddress);
                var encodedDestination = Uri.EscapeDataString(deliveryAddress);
                
                var apiUrl = $"https://maps.googleapis.com/maps/api/distancematrix/json" +
                           $"?origins={encodedOrigin}" +
                           $"&destinations={encodedDestination}" +
                           $"&units=metric" +
                           $"&key={GoogleMapsApiKey}";

                var response = await httpClient.GetStringAsync(apiUrl);
                var data = System.Text.Json.JsonSerializer.Deserialize<GoogleDistanceResponse>(response);

                if (data?.rows?.Length > 0 && data.rows[0]?.elements?.Length > 0)
                {
                    var element = data.rows[0]!.elements![0]!;
                    if (element is { status: "OK", distance: { } d })
                    {
                        var distanceKm = d.value / 1000.0m; // Convert meters to km
                        
                        // Calculate fee: base fee + additional fee per km over 3km
                        var additionalDistance = Math.Max(0, distanceKm - 3);
                        var calculatedFee = baseFee + (additionalDistance * feePerKm);
                        
                        // Cap maximum delivery fee at 100 pesos
                        return Math.Min(calculatedFee, 100m);
                    }
                }
            }
            catch (Exception)
            {
                // If API call fails, return default fee
            }

            return 19m; // Default fee
        }

        /// <summary>
        /// AJAX endpoint to calculate delivery fee
        /// </summary>
        public async Task<IActionResult> OnGetCalculateDeliveryFeeAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return new JsonResult(new { success = false, fee = 19m, message = "Address is required" });
            }

            try
            {
                var fee = await CalculateDeliveryFeeAsync(address);
                return new JsonResult(new { success = true, fee = fee });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, fee = 19m, message = ex.Message });
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                ModelState.AddModelError("", "You must be logged in to place an order.");
                return Page();
            }

            // Always use the saved profile address for checkout, do not trust posted value
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return Page();
            }

            // Override bound address with the user's saved address
            ModelState.Remove(nameof(DeliveryAddress));
            DeliveryAddress = user.Address ?? string.Empty;
            if (string.IsNullOrWhiteSpace(DeliveryAddress))
            {
                ModelState.AddModelError(nameof(DeliveryAddress), "Please set your delivery address in your Profile before checking out.");
            }

            // Custom validation for GCash payment
            if (SelectedPaymentId == 2) // Assuming GCash is ID 2
            {
                if (string.IsNullOrWhiteSpace(GCashNumber))
                {
                    ModelState.AddModelError(nameof(GCashNumber), "GCash number is required for GCash payment.");
                }
            }
            else
            {
                // Clear GCash number if not using GCash
                GCashNumber = null;
                ModelState.Remove(nameof(GCashNumber));
            }

            // Reload cart items for validation
            CartItems = await _context.Carts
                .Where(c => c.UserId == userId)
                .Include(c => c.MenuPrice)
                    .ThenInclude(mp => mp.MenuItem)
                .Include(c => c.MenuPrice)
                    .ThenInclude(mp => mp.Size)
                .Select(c => new CartItemViewModel
                {
                    CartId = c.CartId,
                    PriceId = c.PriceId,
                    ItemName = c.MenuPrice.MenuItem.ItemName,
                    SizeName = c.MenuPrice.Size.SizeName,
                    Image = c.MenuPrice.MenuItem.Image,
                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice
                })
                .ToListAsync();

            if (!CartItems.Any())
            {
                ModelState.AddModelError("", "Your cart is empty.");
                return Page();
            }

            // Recompute delivery fee on the server to avoid tampering
            var serverDeliveryFee = await CalculateDeliveryFeeAsync(DeliveryAddress);
            CalculatedDeliveryFee = serverDeliveryFee;

            if (!ModelState.IsValid)
            {
                PaymentMethods = await _context.Payments.OrderBy(p => p.PaymentId).ToListAsync();
                return Page();
            }

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                // Validate payment method
                var payment = await _context.Payments.FindAsync(SelectedPaymentId);
                if (payment == null)
                {
                    ModelState.AddModelError("", "Invalid payment method.");
                    PaymentMethods = await _context.Payments.OrderBy(p => p.PaymentId).ToListAsync();
                    return Page();
                }

                // Get pending status
                var pendingStatus = await _context.Statuses
                    .FirstOrDefaultAsync(s => s.StatusName == "Pending");
                
                if (pendingStatus == null)
                {
                    ModelState.AddModelError("", "System error: Unable to process order. Please try again.");
                    PaymentMethods = await _context.Payments.OrderBy(p => p.PaymentId).ToListAsync();
                    return Page();
                }

                // Create order
                var order = new Order
                {
                    UserId = userId,
                    StatusId = pendingStatus.StatusId,
                    PaymentId = SelectedPaymentId,
                    OrderDate = DateTime.UtcNow, // store UTC; display PH
                    TotalAmount = Subtotal + CalculatedDeliveryFee, // Include delivery fee in total
                    DeliveryFee = CalculatedDeliveryFee,
                    DeliveryAddress = DeliveryAddress,
                    LastUpdate = DateTime.UtcNow // store UTC; display PH
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Create order items
                var orderItems = CartItems.Select(item => new OrderItem
                {
                    OrderId = order.OrderId,
                    PriceId = item.PriceId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Subtotal = item.Subtotal
                }).ToList();

                _context.OrderItems.AddRange(orderItems);
                await _context.SaveChangesAsync();

                // Clear cart
                var cartItems = await _context.Carts
                    .Where(c => c.UserId == userId)
                    .ToListAsync();
                _context.Carts.RemoveRange(cartItems);
                await _context.SaveChangesAsync();

                // Update user contact info if provided (address managed via Profile only)
                user.PhoneNumber = PhoneNumber;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Order #{order.OrderId} has been placed successfully!";
                TempData["OrderId"] = order.OrderId;
                
                return RedirectToPage("/OrderConfirmation", new { orderId = order.OrderId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An error occurred while processing your order: {ex.Message}");
                PaymentMethods = await _context.Payments.OrderBy(p => p.PaymentId).ToListAsync();
                return Page();
            }
        }

        public class AddCartDto
        {
            public int PriceId { get; set; }
            public int Quantity { get; set; }
        }
    }

    // Google Distance Matrix API response models
    public class GoogleDistanceResponse
    {
        public string status { get; set; } = "";
        public DistanceRow[]? rows { get; set; }
    }

    public class DistanceRow
    {
        public DistanceElement[]? elements { get; set; }
    }

    public class DistanceElement
    {
        public string status { get; set; } = "";
        public DistanceInfo? distance { get; set; }
        public DurationInfo? duration { get; set; }
    }

    public class DistanceInfo
    {
        public string text { get; set; } = "";
        public int value { get; set; }
    }

    public class DurationInfo
    {
        public string text { get; set; } = "";
        public int value { get; set; }
    }
}