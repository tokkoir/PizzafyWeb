using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace PizzafyWeb.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class EditProductModel : PageModel
    {
        private readonly PizzafyDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public EditProductModel(PizzafyDbContext context, IWebHostEnvironment environment)
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

        [BindProperty(SupportsGet = true)]
        public int? Id { get; set; }

        public List<Category> Categories { get; set; } = new();
        public List<Size> AvailableSizes { get; set; } = new();
        public string? ExistingImage { get; set; }
        public string SelectedCategoryName { get; set; } = "";

        public async Task<IActionResult> OnGetAsync()
        {
            if (Id == null)
                return RedirectToPage("/Admin/MenuManagement");

            var cats = await _context.Categories.ToListAsync();
            Categories = cats
                .OrderBy(c => GetCategorySortKey(c.CategoryName))
                .ThenBy(c => c.CategoryName)
                .ToList();

            var menuItem = await _context.MenuItems
                .Include(m => m.MenuPrices)
                .FirstOrDefaultAsync(m => m.MenuItemId == Id);
            if (menuItem == null)
                return RedirectToPage("/Admin/MenuManagement");

            ProductName = menuItem.ItemName;
            Description = menuItem.Description;
            CategoryId = menuItem.CategoryId;
            ExistingImage = menuItem.Image;
            SelectedCategoryName = Categories.FirstOrDefault(c => c.CategoryId == CategoryId)?.CategoryName ?? "";

            var sizes = await _context.Sizes.Where(s => s.CategoryId == CategoryId).ToListAsync();
            AvailableSizes = sizes
                .OrderBy(s => GetSizeSortKey(s.SizeName))
                .ThenBy(s => s.SizeName)
                .ToList();

            var price = menuItem.MenuPrices.FirstOrDefault();
            if (price != null)
            {
                SelectedSizeId = price.SizeId;
                Price = price.UnitPrice;
            }
            return Page();
        }

        public async Task<IActionResult> OnGetSizesAsync(int categoryId)
        {
            var sizes = await _context.Sizes
                .Where(s => s.CategoryId == categoryId)
                .ToListAsync();

            sizes = sizes
                .OrderBy(s => GetSizeSortKey(s.SizeName))
                .ThenBy(s => s.SizeName)
                .ToList();

            return new JsonResult(sizes.Select(s => new { sizeId = s.SizeId, sizeName = s.SizeName }));
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var cats = await _context.Categories.ToListAsync();
            Categories = cats
                .OrderBy(c => GetCategorySortKey(c.CategoryName))
                .ThenBy(c => c.CategoryName)
                .ToList();

            if (Id == null)
                return RedirectToPage("/Admin/MenuManagement");

            var menuItem = await _context.MenuItems.Include(m => m.MenuPrices).FirstOrDefaultAsync(m => m.MenuItemId == Id);
            if (menuItem == null)
                return RedirectToPage("/Admin/MenuManagement");

            if (string.IsNullOrWhiteSpace(ProductName))
                ModelState.AddModelError(nameof(ProductName), "Product name is required");
            if (CategoryId <= 0)
                ModelState.AddModelError(nameof(CategoryId), "Category is required");
            if (SelectedSizeId <= 0)
                ModelState.AddModelError(nameof(SelectedSizeId), "Size is required");
            if (Price <= 0)
                ModelState.AddModelError(nameof(Price), "Price must be greater than 0");

            var selectedSize = await _context.Sizes.Where(s => s.SizeId == SelectedSizeId && s.CategoryId == CategoryId).FirstOrDefaultAsync();
            if (selectedSize == null)
                ModelState.AddModelError(nameof(SelectedSizeId), "Invalid size for the selected category");

            if (!ModelState.IsValid)
            {
                var sizes = await _context.Sizes.Where(s => s.CategoryId == CategoryId).ToListAsync();
                AvailableSizes = sizes
                    .OrderBy(s => GetSizeSortKey(s.SizeName))
                    .ThenBy(s => s.SizeName)
                    .ToList();
                ExistingImage = menuItem.Image;
                SelectedCategoryName = Categories.FirstOrDefault(c => c.CategoryId == CategoryId)?.CategoryName ?? "";
                return Page();
            }

            menuItem.ItemName = ProductName;
            menuItem.Description = Description;
            menuItem.CategoryId = CategoryId;
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
                    var sizes = await _context.Sizes.Where(s => s.CategoryId == CategoryId).ToListAsync();
                    AvailableSizes = sizes
                        .OrderBy(s => GetSizeSortKey(s.SizeName))
                        .ThenBy(s => s.SizeName)
                        .ToList();
                    ExistingImage = menuItem.Image;
                    SelectedCategoryName = Categories.FirstOrDefault(c => c.CategoryId == CategoryId)?.CategoryName ?? "";
                    return Page();
                }
                if (ImageFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("ImageFile", "Image file size must be less than 5MB.");
                    var sizes = await _context.Sizes.Where(s => s.CategoryId == CategoryId).ToListAsync();
                    AvailableSizes = sizes
                        .OrderBy(s => GetSizeSortKey(s.SizeName))
                        .ThenBy(s => s.SizeName)
                        .ToList();
                    ExistingImage = menuItem.Image;
                    SelectedCategoryName = Categories.FirstOrDefault(c => c.CategoryId == CategoryId)?.CategoryName ?? "";
                    return Page();
                }
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var sanitizedProductName = string.Join("", ProductName.Take(20).Where(c => char.IsLetterOrDigit(c) || c == ' ')).Replace(" ", "-").ToLower();
                var imageName = $"{sanitizedProductName}-{timestamp}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, imageName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await ImageFile.CopyToAsync(fileStream);
                }
                menuItem.Image = imageName;
            }
            var menuPrice = menuItem.MenuPrices.FirstOrDefault();
            if (menuPrice != null)
            {
                menuPrice.SizeId = SelectedSizeId;
                menuPrice.UnitPrice = Price;
            }
            else
            {
                menuItem.MenuPrices.Add(new MenuPrice
                {
                    SizeId = SelectedSizeId,
                    UnitPrice = Price
                });
            }
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Product updated successfully!";
            return RedirectToPage("/Admin/MenuManagement", new { success = "product-edited", refresh = "true" });
        }

        private static int GetCategorySortKey(string? name)
        {
            var n = (name ?? string.Empty).Trim().ToLowerInvariant();
            return n switch
            {
                "pizza" => 0,
                "sides" => 1,
                "beverages" => 2,
                _ => 100
            };
        }

        private static int GetSizeSortKey(string? size)
        {
            var s = (size ?? string.Empty).Trim().ToLowerInvariant();
            if (s.Contains("extra small") || s == "xs" || s.Contains("x-small")) return 0;
            if (s.Contains("small")) return 1;
            if (s.Contains("medium") || s == "md") return 2;
            if (s == "large") return 3;
            if (s.Contains("x-large") || s.Contains("extra large") || s == "xl") return 4;
            if (s.Contains("xx-large") || s.Contains("2x") || s == "xxl") return 5;

            var m = Regex.Match(s, @"(\d+\.?\d*)\s*(in|inch|inches|cm|mm|oz)");
            if (m.Success && decimal.TryParse(m.Groups[1].Value, out var v))
            {
                return 100 + (int)(v * 10);
            }
            var m2 = Regex.Match(s, @"^(\d+)$");
            if (m2.Success && int.TryParse(m2.Groups[1].Value, out var n))
            {
                return 100 + n;
            }
            return 1000;
        }
    }
}
