using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;

namespace PizzafyWeb.Pages
{
    public class PizzaProductViewModel
    {
        public int MenuItemId { get; set; }
        public string ItemName { get; set; } = "";
        public string? Description { get; set; }
        public string? Image { get; set; }
        public decimal Price { get; set; } // This will be the starting price (minimum)
        public List<SizePrice> Sizes { get; set; } = new();
        public string CategoryName { get; set; } = "";
        public bool IsAvailable { get; set; } = true;
    }

    public class SizePrice
    {
        public int SizeId { get; set; }
        public string SizeName { get; set; } = "";
        public decimal Price { get; set; }
        public int PriceId { get; set; }
    }

    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly PizzafyDbContext _context;

        public IndexModel(ILogger<IndexModel> logger, PizzafyDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public List<PizzaProductViewModel> PizzaProducts { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
        public int TotalPages { get; set; } = 1;
        public int CurrentPage { get; set; } = 1;
        public bool ShowPagination { get; set; } = false;

        public async Task OnGetAsync()
        {
            // Get all categories
            Categories = await _context.Categories.ToListAsync();
            
            // Load ALL products by default for initial load (no pagination for "all")
            await LoadProductsByCategory("all");
        }

        public async Task<IActionResult> OnGetTestDataAsync(string category = "beverages")
        {
            try
            {
                var allItems = await _context.MenuItems
                    .Include(m => m.Category)
                    .Where(m => m.Category.CategoryName.ToLower() == category.ToLower())
                    .Select(m => new { m.MenuItemId, m.ItemName, m.Category.CategoryName })
                    .ToListAsync();

                return new JsonResult(new
                {
                    category = category,
                    totalItems = allItems.Count,
                    items = allItems,
                    message = $"Found {allItems.Count} items in {category} category"
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { error = ex.Message });
            }
        }

        public async Task<IActionResult> OnGetProductsByCategoryAsync(string category = "all", string search = "", int page = 1)
        {
            try
            {
                _logger.LogInformation("Loading products for category: {Category}, page: {Page}, search: {Search}", category, page, search);
                
                var query = _context.MenuItems
                    .Include(m => m.MenuPrices)
                        .ThenInclude(mp => mp.Size)
                    .Include(m => m.Category)
                    .AsQueryable();

                // Filter by category if specified and not "all"
                if (!string.IsNullOrEmpty(category) && category != "all")
                {
                    query = query.Where(m => m.Category.CategoryName.ToLower() == category.ToLower());
                }

                // Filter by search term if specified
                if (!string.IsNullOrEmpty(search))
                {
                    var s = search.ToLower();
                    query = query.Where(m => m.ItemName.ToLower().Contains(s));
                }

                var items = await query.ToListAsync();

                // Group strictly by ItemName and unify sizes across all matching menu items.
                var uniqueProducts = items
                    .GroupBy(m => m.ItemName)
                    .Select(g =>
                    {
                        var rep = g.First();
                        // unify sizes: choose the entry per size with the lowest price, and keep its priceId
                        var allSizes = g.SelectMany(m => m.MenuPrices)
                            .Select(mp => new { priceId = mp.PriceId, sizeId = mp.Size.SizeId, sizeName = mp.Size.SizeName, price = mp.UnitPrice })
                            .GroupBy(x => new { x.sizeId, x.sizeName })
                            .Select(gg => gg.OrderBy(x => x.price).First())
                            .OrderBy(x => x.price)
                            .ToList();

                        return new
                        {
                            menuItemId = rep.MenuItemId,
                            itemName = rep.ItemName,
                            description = rep.Description,
                            image = g.Select(x => x.Image).FirstOrDefault(img => !string.IsNullOrWhiteSpace(img)) ?? rep.Image,
                            categoryName = rep.Category.CategoryName,
                            price = allSizes.Min(s => s.price),
                            sizes = allSizes,
                            isAvailable = g.Any(x => x.IsAvailable)
                        };
                    })
                    .ToList();

                // Sort: available first, then not available (only for 'all' category)
                if (category == "all")
                {
                    uniqueProducts = uniqueProducts
                        .OrderByDescending(p => p.isAvailable)
                        .ThenBy(p => p.itemName)
                        .ToList();
                }

                // PAGINATION based on UNIQUE products only
                const int pageSize = 4;
                int totalCount = uniqueProducts.Count;
                int totalPages = 1;
                bool showPagination = false;

                List<object> pageProducts;
                if (category == "all")
                {
                    pageProducts = uniqueProducts.Cast<object>().ToList();
                    showPagination = false;
                    totalPages = 1;
                    page = 1;
                }
                else
                {
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                    page = Math.Clamp(page, 1, Math.Max(1, totalPages));
                    showPagination = totalCount > pageSize;

                    pageProducts = uniqueProducts
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Cast<object>()
                        .ToList();
                }

                _logger.LogInformation("Returning {Count} unique products. totalPages={TotalPages}, showPagination={Show}", pageProducts.Count, totalPages, showPagination);
                
                var result = new
                {
                    products = pageProducts,
                    totalPages = totalPages,
                    currentPage = page,
                    showPagination = showPagination,
                    totalCount = totalCount,
                    category = category
                };

                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products by category");
                return new JsonResult(new
                {
                    products = new List<object>(),
                    totalPages = 1,
                    currentPage = 1,
                    showPagination = false,
                    totalCount = 0,
                    category = category,
                    error = ex.Message
                });
            }
        }

        private async Task LoadProductsByCategory(string category = "all")
        {
            try
            {
                var query = _context.MenuItems
                    .Include(m => m.MenuPrices)
                        .ThenInclude(mp => mp.Size)
                    .Include(m => m.Category)
                    .AsQueryable();

                // Filter by category if not "all"
                if (!string.IsNullOrEmpty(category) && category != "all")
                {
                    query = query.Where(m => m.Category.CategoryName.ToLower() == category.ToLower());
                }

                var items = await query.ToListAsync();

                // Map to view models - unify sizes across all menu items with the same ItemName
                var products = items
                    .GroupBy(m => m.ItemName)
                    .Select(g =>
                    {
                        var rep = g.First();
                        var allSizes = g.SelectMany(m => m.MenuPrices)
                            .Select(mp => new SizePrice
                            {
                                SizeId = mp.Size.SizeId,
                                SizeName = mp.Size.SizeName,
                                Price = mp.UnitPrice,
                                PriceId = mp.PriceId
                            })
                            .GroupBy(s => new { s.SizeId, s.SizeName })
                            .Select(gg => gg.OrderBy(s => s.Price).First())
                            .OrderBy(s => s.Price)
                            .ToList();

                        return new PizzaProductViewModel
                        {
                            MenuItemId = rep.MenuItemId,
                            ItemName = rep.ItemName,
                            Description = rep.Description,
                            Image = g.Select(x => x.Image).FirstOrDefault(img => !string.IsNullOrWhiteSpace(img)) ?? rep.Image,
                            Price = allSizes.Min(s => s.Price),
                            CategoryName = rep.Category.CategoryName,
                            Sizes = allSizes,
                            IsAvailable = g.Any(x => x.IsAvailable)
                        };
                    })
                    .ToList();

                // Sort: available first, then not available (only for 'all' category)
                if (category == "all")
                {
                    products = products
                        .OrderByDescending(p => p.IsAvailable)
                        .ThenBy(p => p.ItemName)
                        .ToList();
                }

                PizzaProducts = products;

                // No pagination for "all" category on initial load
                ShowPagination = false;
                CurrentPage = 1;
                TotalPages = 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products");
                PizzaProducts = new List<PizzaProductViewModel>();
            }
        }
    }
}
