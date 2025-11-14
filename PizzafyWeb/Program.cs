using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using PizzafyWeb.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Configure cart settings
builder.Services.Configure<CartSettings>(
    builder.Configuration.GetSection("CartSettings"));

// Configure HTTPS
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
    options.HttpsPort = 7042;
});

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Require HTTPS for cookies
});

// Add Entity Framework and MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<PizzafyDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// Add Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.AccessDeniedPath = "/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
        options.SlidingExpiration = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Require HTTPS for auth cookies
        options.Cookie.SameSite = SameSiteMode.Strict;
        // Ensure API endpoints return proper codes instead of HTML redirects
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api"))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }
                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api"))
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                }
                ctx.Response.Redirect(ctx.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

// Seed the database with error handling
try
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<PizzafyDbContext>();
        var seeder = new DatabaseSeeder(context);
        await seeder.SeedAsync();
    }
}
catch (Exception ex)
{
    // Log the error but don't stop the application
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while seeding the database.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Force HTTPS redirection
app.UseHttpsRedirection();

// Enhanced static files configuration
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Set cache headers for better performance
        const int durationInSeconds = 60 * 60 * 24 * 30; // 30 days for most static files
        ctx.Context.Response.Headers.Append("Cache-Control", $"public, max-age={durationInSeconds}");
        
        // But for product images, use shorter cache for easier updates
        if (ctx.Context.Request.Path.StartsWithSegments("/images/products"))
        {
            const int imageCacheDuration = 60 * 60 * 24; // 1 day for product images
            ctx.Context.Response.Headers["Cache-Control"] = $"public, max-age={imageCacheDuration}";
            
            // Add ETag for better caching
            var etag = $"\"{ctx.Context.Request.Path.Value?.GetHashCode():X}\"";
            ctx.Context.Response.Headers.Append("ETag", etag);
        }
    }
});

app.UseRouting();
app.UseSession(); // Add session middleware

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Minimal API for cart count (unique items based on distinct MenuItemId)
app.MapGet("/api/cart/count", async (HttpContext http, PizzafyWeb.Data.PizzafyDbContext db) =>
{
    if (!http.User.Identity?.IsAuthenticated ?? true)
        return Results.Json(new { count = 0 });

    var userIdClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userIdClaim)) return Results.Json(new { count = 0 });
    var userId = int.Parse(userIdClaim);

    var uniqueCount = await db.Carts
        .Where(c => c.UserId == userId)
        .Join(db.MenuPrices,
              c => c.PriceId,
              mp => mp.PriceId,
              (c, mp) => mp.MenuItemId)
        .Distinct()
        .CountAsync();

    return Results.Json(new { count = uniqueCount });
});

// Addresses API
app.MapGet("/api/user/addresses", async (HttpContext http, PizzafyDbContext db) =>
{
    if (!http.User.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
    if (!int.TryParse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
        return Results.Unauthorized();

    var list = await db.UserAddresses
        .Where(a => a.UserId == userId)
        .OrderByDescending(a => a.IsDefault)
        .ThenByDescending(a => a.CreatedAt)
        .Select(a => new
        {
            a.Id,
            a.StreetAddress,
            a.Barangay,
            a.City,
            a.Landmark,
            a.IsDefault
        })
        .ToListAsync();
    return Results.Json(list);
}).RequireAuthorization();

app.MapPost("/api/user/addresses", async (HttpContext http, PizzafyDbContext db, UserAddress dto) =>
{
    if (!http.User.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
    if (!int.TryParse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(dto.StreetAddress) || string.IsNullOrWhiteSpace(dto.Barangay))
        return Results.BadRequest(new { message = "Street and barangay are required." });

    dto.UserId = userId;
    dto.City = "Cebu City";
    dto.CreatedAt = DateTime.UtcNow;
    dto.UpdatedAt = DateTime.UtcNow;

    if (dto.IsDefault)
    {
        // Clear other defaults
        var others = await db.UserAddresses.Where(a => a.UserId == userId && a.IsDefault).ToListAsync();
        foreach (var a in others) a.IsDefault = false;
    }

    db.UserAddresses.Add(dto);
    await db.SaveChangesAsync();
    return Results.Json(new { ok = true, id = dto.Id });
}).RequireAuthorization();

app.MapPut("/api/user/addresses/{id:int}", async (HttpContext http, PizzafyDbContext db, int id, UserAddress dto) =>
{
    if (!http.User.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
    if (!int.TryParse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
        return Results.Unauthorized();

    var addr = await db.UserAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
    if (addr == null) return Results.NotFound();

    addr.StreetAddress = dto.StreetAddress;
    addr.Barangay = dto.Barangay;
    addr.City = "Cebu City";
    addr.Landmark = dto.Landmark;
    addr.UpdatedAt = DateTime.UtcNow;

    if (dto.IsDefault && !addr.IsDefault)
    {
        var others = await db.UserAddresses.Where(a => a.UserId == userId && a.IsDefault).ToListAsync();
        foreach (var a in others) a.IsDefault = false;
        addr.IsDefault = true;
    }
    else if (!dto.IsDefault && addr.IsDefault)
    {
        addr.IsDefault = false;
    }

    await db.SaveChangesAsync();
    return Results.Json(new { ok = true });
}).RequireAuthorization();

app.MapDelete("/api/user/addresses/{id:int}", async (HttpContext http, PizzafyDbContext db, int id) =>
{
    if (!http.User.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
    if (!int.TryParse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
        return Results.Unauthorized();

    var addr = await db.UserAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
    if (addr == null) return Results.NotFound();

    db.UserAddresses.Remove(addr);
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

app.MapPut("/api/user/addresses/{id:int}/default", async (HttpContext http, PizzafyDbContext db, int id) =>
{
    if (!http.User.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
    if (!int.TryParse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
        return Results.Unauthorized();

    var addr = await db.UserAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
    if (addr == null) return Results.NotFound();

    var others = await db.UserAddresses.Where(a => a.UserId == userId && a.IsDefault).ToListAsync();
    foreach (var a in others) a.IsDefault = false;
    addr.IsDefault = true;
    await db.SaveChangesAsync();

    return Results.Ok();
}).RequireAuthorization();

// Barangays API
app.MapGet("/api/barangays", async (PizzafyDbContext db, string? city) =>
{
    var c = string.IsNullOrWhiteSpace(city) ? "Cebu City" : city;
    var list = await db.Barangays
        .Where(b => b.City == c)
        .OrderBy(b => b.Name)
        .Select(b => b.Name)
        .ToListAsync();
    return Results.Json(list);
});

// Checkout API (delivery only)
app.MapPost("/api/orders/checkout", async (HttpContext http, PizzafyDbContext db) =>
{
    if (!http.User.Identity?.IsAuthenticated ?? true)
        return Results.Unauthorized();
    var userIdClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!int.TryParse(userIdClaim, out var userId))
        return Results.Unauthorized();

    var req = await http.Request.ReadFromJsonAsync<CheckoutRequest>();
    if (req == null) return Results.BadRequest(new { message = "Invalid request" });

    // Enforce delivery only
    var orderType = "delivery";

    if (!string.IsNullOrWhiteSpace(req.special_instructions) && req.special_instructions!.Length > 250)
        return Results.BadRequest(new { message = "Special instructions exceed 250 characters" });

    if (string.IsNullOrWhiteSpace(req.payment_method) || (req.payment_method != "cash" && req.payment_method != "gcash"))
        return Results.BadRequest(new { message = "Invalid payment method" });

    // Validate phone
    if (string.IsNullOrWhiteSpace(req.phone_number) || !(System.Text.RegularExpressions.Regex.IsMatch(req.phone_number, @"^(09\d{9}|\+639\d{9})$")))
        return Results.BadRequest(new { message = "Invalid phone number" });

    // Load cart
    var carts = await db.Carts
        .Where(c => c.UserId == userId)
        .Include(c => c.MenuPrice)
            .ThenInclude(mp => mp.MenuItem)
        .ToListAsync();

    if (!carts.Any())
        return Results.BadRequest(new { message = "Your cart is empty" });

    // Compute amounts
    decimal subtotal = carts.Sum(c => c.UnitPrice * c.Quantity);
    decimal deliveryFee = 19m;

    // Resolve payment id robustly
    int? paymentId = null;
    Payment? payment = null;
    if (req.payment_method == "cash")
    {
        payment = await db.Payments.FirstOrDefaultAsync(p =>
            EF.Functions.Like(p.PaymentName, "%cash%") || EF.Functions.Like(p.PaymentName, "%cod%"));
    }
    else if (req.payment_method == "gcash")
    {
        payment = await db.Payments.FirstOrDefaultAsync(p =>
            EF.Functions.Like(p.PaymentName, "%gcash%"));
    }

    if (payment != null)
    {
        paymentId = payment.PaymentId;
    }

    if (paymentId == null)
    {
        return Results.BadRequest(new { message = "Payment method is not configured. Please contact support." });
    }

    // Get Pending status
    var pending = await db.Statuses.FirstOrDefaultAsync(s => s.StatusName == "Pending");
    if (pending == null) return Results.BadRequest(new { message = "System error: Missing order status." });

    // Start transaction
    using var tx = await db.Database.BeginTransactionAsync();
    try
    {
        var order = new Order
        {
            UserId = userId,
            StatusId = pending.StatusId,
            PaymentId = paymentId.Value,
            OrderDate = DateTime.UtcNow,
            TotalAmount = subtotal + deliveryFee,
            DeliveryFee = deliveryFee,
            OrderType = orderType,
            SpecialInstructions = req.special_instructions,
            PaymentMethod = req.payment_method, // store canonical code: 'cash' | 'gcash'
            LastUpdate = DateTime.UtcNow
        };

        // Delivery-only validation
        if (req.address_id == null)
            return Results.BadRequest(new { message = "Delivery address is required" });
        var addr = await db.UserAddresses.FirstOrDefaultAsync(a => a.Id == req.address_id && a.UserId == userId);
        if (addr == null) return Results.BadRequest(new { message = "Invalid address" });
        if (!string.Equals(addr.City, "Cebu City", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { message = "Delivery is only available within Cebu City" });

        order.AddressId = addr.Id;
        order.DeliveryAddress = $"{addr.StreetAddress}, {addr.Barangay}, {addr.City}";
        order.DeliveryStreet = addr.StreetAddress;
        order.DeliveryBarangay = addr.Barangay;
        order.DeliveryCity = addr.City;
        order.DeliveryLandmark = addr.Landmark;

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Order items
        var orderItems = carts.Select(c => new OrderItem
        {
            OrderId = order.OrderId,
            PriceId = c.PriceId,
            Quantity = c.Quantity,
            UnitPrice = c.UnitPrice,
            Subtotal = c.UnitPrice * c.Quantity
        }).ToList();
        db.OrderItems.AddRange(orderItems);
        await db.SaveChangesAsync();

        // Clear cart
        db.Carts.RemoveRange(carts);
        await db.SaveChangesAsync();

        // Update user's phone if provided
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user != null)
        {
            user.PhoneNumber = req.phone_number;
            await db.SaveChangesAsync();
        }

        await tx.CommitAsync();
        return Results.Json(new { success = true, orderId = order.OrderId });
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
}).RequireAuthorization();

app.Run();

public class CheckoutRequest
{
    public string? order_type { get; set; }
    public int? address_id { get; set; }
    public string? delivery_street { get; set; }
    public string? delivery_barangay { get; set; }
    public string? delivery_city { get; set; }
    public string? delivery_landmark { get; set; }
    public string? pickup_date { get; set; }
    public string? pickup_time { get; set; }
    public string? special_instructions { get; set; }
    public string? payment_method { get; set; }
    public bool? save_address { get; set; }
    public bool? set_as_default { get; set; }
    public string? phone_number { get; set; }
    public string? customer_name { get; set; }
    public string? gcash_number { get; set; }
}
