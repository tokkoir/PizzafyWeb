using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

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

// Minimal API for cart count
app.MapGet("/api/cart/count", async (HttpContext http, PizzafyWeb.Data.PizzafyDbContext db) =>
{
    if (!http.User.Identity?.IsAuthenticated ?? true)
        return Results.Json(new { count = 0 });

    var userIdClaim = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userIdClaim)) return Results.Json(new { count = 0 });
    var userId = int.Parse(userIdClaim);
    var count = await db.Carts.Where(c => c.UserId == userId).SumAsync(c => (int?)c.Quantity) ?? 0;
    return Results.Json(new { count });
});

app.Run();
