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

            Categories = await _context.Categories.ToListAsync();
            var menuItem = await _context.MenuItems
                .Include(m => m.MenuPrices)
                .FirstOrDefaultAsync(m => m.MenuItemId == Id);
            if (menuItem == null)
                return RedirectToPage("/Admin/MenuManagement");

            ProductName = menuItem.ItemName;
            CategoryId = menuItem.CategoryId;
            ExistingImage = menuItem.Image;
            SelectedCategoryName = Categories.FirstOrDefault(c => c.CategoryId == CategoryId)?.CategoryName ?? "";
            AvailableSizes = await _context.Sizes.Where(s => s.CategoryId == CategoryId).OrderBy(s => s.SizeName).ToListAsync();
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
                .OrderBy(s => s.SizeName)
                .ToListAsync();
            return new JsonResult(sizes.Select(s => new { sizeId = s.SizeId, sizeName = s.SizeName }));
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Categories = await _context.Categories.ToListAsync();
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
                AvailableSizes = await _context.Sizes.Where(s => s.CategoryId == CategoryId).OrderBy(s => s.SizeName).ToListAsync();
                ExistingImage = menuItem.Image;
                SelectedCategoryName = Categories.FirstOrDefault(c => c.CategoryId == CategoryId)?.CategoryName ?? "";
                return Page();
            }
            menuItem.ItemName = ProductName;
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
                    AvailableSizes = await _context.Sizes.Where(s => s.CategoryId == CategoryId).OrderBy(s => s.SizeName).ToListAsync();
                    ExistingImage = menuItem.Image;
                    SelectedCategoryName = Categories.FirstOrDefault(c => c.CategoryId == CategoryId)?.CategoryName ?? "";
                    return Page();
                }
                if (ImageFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("ImageFile", "Image file size must be less than 5MB.");
                    AvailableSizes = await _context.Sizes.Where(s => s.CategoryId == CategoryId).OrderBy(s => s.SizeName).ToListAsync();
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
    }
}
