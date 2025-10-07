using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace PizzafyWeb.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class AddProductModel : PageModel
    {
        private readonly PizzafyDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AddProductModel(PizzafyDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [BindProperty]
        [Required(ErrorMessage = "Product name is required")]
        public string ProductName { get; set; } = string.Empty;

        [BindProperty]
        [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Category is required")]
        public int CategoryId { get; set; }

        [BindProperty]
        public IFormFile? ImageFile { get; set; }

        [BindProperty]
        public int SelectedSizeId { get; set; }

        [BindProperty]
        public decimal Price { get; set; }

        public List<Category> Categories { get; set; } = new();
        public List<Size> AvailableSizes { get; set; } = new();

        public async Task OnGetAsync()
        {
            try
            {
                Categories = await _context.Categories.ToListAsync();
                
                // Initialize with empty sizes (will be populated by JavaScript when category is selected)
                AvailableSizes = new List<Size>();
            }
            catch (Exception)
            {
                Categories = new List<Category>();
                AvailableSizes = new List<Size>();
            }
        }

        public async Task<IActionResult> OnGetSizesAsync(int categoryId)
        {
            try
            {
                var sizes = await _context.Sizes
                    .Where(s => s.CategoryId == categoryId)
                    .OrderBy(s => s.SizeName)
                    .ToListAsync();

                return new JsonResult(sizes.Select(s => new { 
                    sizeId = s.SizeId, 
                    sizeName = s.SizeName 
                }));
            }
            catch (Exception)
            {
                return new JsonResult(new List<object>());
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                Categories = await _context.Categories.ToListAsync();
                ModelState.Remove("MenuItem.Category");
                ModelState.Remove("MenuItem.User");
                ModelState.Remove("MenuItem.MenuPrices");

                if (string.IsNullOrWhiteSpace(ProductName))
                    ModelState.AddModelError(nameof(ProductName), "Product name is required");
                if (CategoryId <= 0)
                    ModelState.AddModelError(nameof(CategoryId), "Category is required");
                if (SelectedSizeId <= 0)
                    ModelState.AddModelError(nameof(SelectedSizeId), "Size is required");
                if (Price <= 0)
                    ModelState.AddModelError(nameof(Price), "Price must be greater than 0");

                var selectedSize = await _context.Sizes
                    .Where(s => s.SizeId == SelectedSizeId && s.CategoryId == CategoryId)
                    .FirstOrDefaultAsync();
                if (selectedSize == null)
                    ModelState.AddModelError(nameof(SelectedSizeId), "Invalid size for the selected category");

                // Get the selected category name
                var selectedCategory = Categories.FirstOrDefault(c => c.CategoryId == CategoryId)?.CategoryName.ToLower();

                // Strictly require image for pizza category
                if (selectedCategory == "pizza" && (ImageFile == null || ImageFile.Length == 0))
                {
                    ModelState.AddModelError("ImageFile", "Image is required for pizza products.");
                }

                if (!ModelState.IsValid)
                {
                    AvailableSizes = await _context.Sizes
                        .Where(s => s.CategoryId == CategoryId)
                        .OrderBy(s => s.SizeName)
                        .ToListAsync();
                    return Page();
                }

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    ModelState.AddModelError("", "Unable to identify current user. Please login again.");
                    return Page();
                }

                // Save image if uploaded
                string? imageName = null;
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "products");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var fileExtension = Path.GetExtension(ImageFile.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("ImageFile", "Please upload a valid image file (JPG, PNG, GIF, WebP).");
                        AvailableSizes = await _context.Sizes
                            .Where(s => s.CategoryId == CategoryId)
                            .OrderBy(s => s.SizeName)
                            .ToListAsync();
                        return Page();
                    }
                    if (ImageFile.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ImageFile", "Image file size must be less than 5MB.");
                        AvailableSizes = await _context.Sizes
                            .Where(s => s.CategoryId == CategoryId)
                            .OrderBy(s => s.SizeName)
                            .ToListAsync();
                        return Page();
                    }
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var sanitizedProductName = string.Join("", ProductName.Take(20).Where(c => char.IsLetterOrDigit(c) || c == ' ')).Replace(" ", "-").ToLower();
                    imageName = $"{sanitizedProductName}-{timestamp}{fileExtension}";
                    var filePath = Path.Combine(uploadsFolder, imageName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }
                }

                var menuItem = new MenuItem
                {
                    ItemName = ProductName,
                    Description = Description,
                    CategoryId = CategoryId,
                    UserId = userId,
                    Image = imageName // null if not uploaded
                };

                _context.MenuItems.Add(menuItem);
                await _context.SaveChangesAsync();

                var menuPrice = new MenuPrice
                {
                    MenuItemId = menuItem.MenuItemId,
                    SizeId = SelectedSizeId,
                    UnitPrice = Price
                };
                _context.MenuPrices.Add(menuPrice);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Product added successfully!";
                TempData["NewProductId"] = menuItem.MenuItemId;
                TempData["NewProductImage"] = imageName;
                TempData["ProductAdded"] = "true";
                return RedirectToPage("/Admin/MenuManagement", new { success = "product-added", refresh = "true" });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"An error occurred while adding the product: {ex.Message}");
                try
                {
                    Categories = await _context.Categories.ToListAsync();
                    if (CategoryId > 0)
                    {
                        AvailableSizes = await _context.Sizes
                            .Where(s => s.CategoryId == CategoryId)
                            .OrderBy(s => s.SizeName)
                            .ToListAsync();
                    }
                }
                catch
                {
                    Categories = new List<Category>();
                    AvailableSizes = new List<Size>();
                }
                return Page();
            }
        }
    }
}