using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using System.Security.Cryptography;
using System.Text;

namespace PizzafyWeb.Services
{
    public class DatabaseSeeder
    {
        private readonly PizzafyDbContext _context;

        public DatabaseSeeder(PizzafyDbContext context)
        {
            _context = context;
        }

        public async Task SeedAsync()
        {
            try
            {
                // Check if database exists and create if not
                await _context.Database.EnsureCreatedAsync();

                // Seed Categories
                if (!await _context.Categories.AnyAsync())
                {
                    var categories = new List<Category>
                    {
                        new Category { CategoryName = "Pizza" },
                        new Category { CategoryName = "Sides" },
                        new Category { CategoryName = "Beverages" }
                    };

                    _context.Categories.AddRange(categories);
                    await _context.SaveChangesAsync();
                }

                // Seed Sizes
                if (!await _context.Sizes.AnyAsync())
                {
                    var categories = await _context.Categories.ToListAsync();
                    var pizzaCategory = categories.First(c => c.CategoryName == "Pizza");
                    var sidesCategory = categories.First(c => c.CategoryName == "Sides");
                    var beveragesCategory = categories.First(c => c.CategoryName == "Beverages");

                    var sizes = new List<Size>
                    {
                        // Pizza sizes
                        new Size { CategoryId = pizzaCategory.CategoryId, SizeName = "9 in" },
                        new Size { CategoryId = pizzaCategory.CategoryId, SizeName = "12 in" },
                        new Size { CategoryId = pizzaCategory.CategoryId, SizeName = "14 in" },
                        new Size { CategoryId = pizzaCategory.CategoryId, SizeName = "16 in" },
                        new Size { CategoryId = pizzaCategory.CategoryId, SizeName = "18 in" },
                        
                        // Sides sizes
                        new Size { CategoryId = sidesCategory.CategoryId, SizeName = "Small" },
                        new Size { CategoryId = sidesCategory.CategoryId, SizeName = "Medium" },
                        new Size { CategoryId = sidesCategory.CategoryId, SizeName = "Large" },
                        
                        // Beverages sizes
                        new Size { CategoryId = beveragesCategory.CategoryId, SizeName = "Small (8 oz)" },
                        new Size { CategoryId = beveragesCategory.CategoryId, SizeName = "Medium (12 oz)" },
                        new Size { CategoryId = beveragesCategory.CategoryId, SizeName = "Large (16 oz)" }
                    };

                    _context.Sizes.AddRange(sizes);
                    await _context.SaveChangesAsync();
                }

                // Seed Users
                if (!await _context.Users.AnyAsync())
                {
                    var users = new List<User>
                    {
                        new User
                        {
                            Username = "admin",
                            FirstName = "Admin",
                            LastName = "User",
                            Password = HashPassword("admin123"), // Hash the password
                            UserType = UserType.Admin,
                            CreatedAt = DateTime.Now,
                            PhoneNumber = "123-456-7890",
                            Address = "Admin Office, Company Building"
                        },
                        new User
                        {
                            Username = "customer1",
                            FirstName = "John",
                            LastName = "Doe",
                            Password = HashPassword("customer123"), // Hash the password
                            UserType = UserType.Customer,
                            CreatedAt = DateTime.Now,
                            PhoneNumber = "123-456-7890",
                            Address = "123 Main St, City, State"
                        }
                    };

                    _context.Users.AddRange(users);
                    await _context.SaveChangesAsync();
                }

                // Seed Sample Menu Items
                if (!await _context.MenuItems.AnyAsync())
                {
                    var categories = await _context.Categories.ToListAsync();
                    var sizes = await _context.Sizes.ToListAsync();
                    var adminUser = await _context.Users.FirstAsync(u => u.UserType == UserType.Admin);

                    var pizzaCategory = categories.First(c => c.CategoryName == "Pizza");
                    var sidesCategory = categories.First(c => c.CategoryName == "Sides");
                    var beveragesCategory = categories.First(c => c.CategoryName == "Beverages");

                    // Sample menu items - each unique item name will have multiple sizes/prices
                    // NOTE: If duplicate item names are added, they will be consolidated into one card
                    var menuItems = new List<MenuItem>
                    {
                        // Pizza items (8 unique items)
                        new MenuItem { ItemName = "Margherita Pizza", Description = "Fresh mozzarella, tomatoes, and basil", CategoryId = pizzaCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Pepperoni Pizza", Description = "Classic pepperoni with mozzarella cheese", CategoryId = pizzaCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Hawaiian Pizza", Description = "Ham & Pineapple", CategoryId = pizzaCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Meat Lovers Pizza", Description = "Pepperoni, sausage, bacon, ham and cheese", CategoryId = pizzaCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "BBQ Chicken Pizza", Description = "Spicy barbecue sauce, diced chicken, peppers, onion, and cilantro", CategoryId = pizzaCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Veggie Supreme Pizza", Description = "Bell peppers, mushrooms, onions, olives, and tomatoes", CategoryId = pizzaCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Four Cheese Pizza", Description = "Mozzarella, cheddar, parmesan, and provolone", CategoryId = pizzaCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Spinach & Feta Pizza", Description = "Red onion, spinach, feta cheese and mushrooms", CategoryId = pizzaCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        
                        // Sides items (6 unique items)
                        new MenuItem { ItemName = "Garlic Bread", Description = "Fresh baked bread with garlic butter", CategoryId = sidesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Chicken Wings", Description = "Crispy chicken wings with your choice of sauce", CategoryId = sidesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Caesar Salad", Description = "Fresh romaine lettuce with caesar dressing", CategoryId = sidesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Mozzarella Sticks", Description = "Golden fried mozzarella sticks with marinara sauce", CategoryId = sidesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Onion Rings", Description = "Crispy battered onion rings", CategoryId = sidesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Potato Wedges", Description = "Seasoned potato wedges with sour cream", CategoryId = sidesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        
                        // Beverages items (5 unique items) 
                        // Example: If admin adds another "Sprite" with different size, it will be consolidated
                        new MenuItem { ItemName = "Coca Cola", Description = "Classic refreshing cola drink", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Sprite", Description = "Lemon-lime flavored soft drink", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Orange Juice", Description = "Fresh squeezed orange juice", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Iced Tea", Description = "Refreshing iced tea with lemon", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Water", Description = "Pure drinking water", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        
                        // EXAMPLE: Adding duplicate "Sprite" entries to demonstrate consolidation
                        // These will be merged into a single card with multiple sizes/prices
                        new MenuItem { ItemName = "Sprite", Description = "Lemon-lime flavored soft drink", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Sprite", Description = "Lemon-lime flavored soft drink", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" }
                    };

                    _context.MenuItems.AddRange(menuItems);
                    await _context.SaveChangesAsync();

                    // Add multiple prices for each item (different sizes)
                    var menuPrices = new List<MenuPrice>();
                    
                    // Get sizes for each category
                    var pizzaSizes = sizes.Where(s => s.CategoryId == pizzaCategory.CategoryId).ToList();
                    var sidesSizes = sizes.Where(s => s.CategoryId == sidesCategory.CategoryId).ToList();
                    var beverageSizes = sizes.Where(s => s.CategoryId == beveragesCategory.CategoryId).ToList();

                    // Add prices for pizza items (multiple sizes per item)
                    var pizzaItems = menuItems.Where(m => m.CategoryId == pizzaCategory.CategoryId).ToList();
                    for (int i = 0; i < pizzaItems.Count; i++)
                    {
                        var basePrice = new decimal[] { 12.99m, 14.99m, 15.99m, 17.99m, 16.99m, 13.99m, 15.99m, 14.99m }[i];
                        
                        // Add price for each pizza size
                        foreach (var size in pizzaSizes)
                        {
                            var sizeMultiplier = size.SizeName switch
                            {
                                "9 in" => 0.8m,
                                "12 in" => 1.0m,
                                "14 in" => 1.3m,
                                "16 in" => 1.6m,
                                "18 in" => 2.0m,
                                _ => 1.0m
                            };
                            
                            menuPrices.Add(new MenuPrice
                            {
                                MenuItemId = pizzaItems[i].MenuItemId,
                                SizeId = size.SizeId,
                                UnitPrice = Math.Round(basePrice * sizeMultiplier, 2)
                            });
                        }
                    }
                    
                    // Add prices for sides items (multiple sizes per item)
                    var sidesItems = menuItems.Where(m => m.CategoryId == sidesCategory.CategoryId).ToList();
                    for (int i = 0; i < sidesItems.Count; i++)
                    {
                        var basePrice = new decimal[] { 5.99m, 8.99m, 7.99m, 6.99m, 5.99m, 6.99m }[i];
                        
                        // Add price for each sides size
                        foreach (var size in sidesSizes)
                        {
                            var sizeMultiplier = size.SizeName switch
                            {
                                "Small" => 0.8m,
                                "Medium" => 1.0m,
                                "Large" => 1.4m,
                                _ => 1.0m
                            };
                            
                            menuPrices.Add(new MenuPrice
                            {
                                MenuItemId = sidesItems[i].MenuItemId,
                                SizeId = size.SizeId,
                                UnitPrice = Math.Round(basePrice * sizeMultiplier, 2)
                            });
                        }
                    }
                    
                    // Add prices for beverages items (multiple sizes per item)
                    var beverageItems = menuItems.Where(m => m.CategoryId == beveragesCategory.CategoryId).ToList();
                    
                    // Handle regular beverage items
                    var regularBeverages = beverageItems.Take(5).ToList(); // First 5 beverages
                    for (int i = 0; i < regularBeverages.Count; i++)
                    {
                        var basePrice = new decimal[] { 2.99m, 2.99m, 3.99m, 2.49m, 1.99m }[i];
                        
                        // Add price for each beverage size
                        foreach (var size in beverageSizes)
                        {
                            var sizeMultiplier = size.SizeName switch
                            {
                                "Small (8 oz)" => 0.8m,
                                "Medium (12 oz)" => 1.0m,
                                "Large (16 oz)" => 1.3m,
                                _ => 1.0m
                            };
                            
                            menuPrices.Add(new MenuPrice
                            {
                                MenuItemId = regularBeverages[i].MenuItemId,
                                SizeId = size.SizeId,
                                UnitPrice = Math.Round(basePrice * sizeMultiplier, 2)
                            });
                        }
                    }
                    
                    // Handle duplicate Sprite entries with custom pricing
                    var duplicateSprites = beverageItems.Where(m => m.ItemName == "Sprite").Skip(1).ToList();
                    foreach (var sprite in duplicateSprites)
                    {
                        // Add custom prices for demonstration - Small: $30, Medium: $40, Large: $50
                        var customPrices = new Dictionary<string, decimal>
                        {
                            { "Small (8 oz)", 30.00m },
                            { "Medium (12 oz)", 40.00m },
                            { "Large (16 oz)", 50.00m }
                        };
                        
                        foreach (var size in beverageSizes)
                        {
                            if (customPrices.ContainsKey(size.SizeName))
                            {
                                menuPrices.Add(new MenuPrice
                                {
                                    MenuItemId = sprite.MenuItemId,
                                    SizeId = size.SizeId,
                                    UnitPrice = customPrices[size.SizeName]
                                });
                            }
                        }
                    }

                    _context.MenuPrices.AddRange(menuPrices);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // Log the exception in production
                Console.WriteLine($"Error seeding database: {ex.Message}");
            }
        }

        private string HashPassword(string password)
        {
            using (var md5 = MD5.Create())
            {
                var hashedBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToHexString(hashedBytes).ToLower();
            }
        }
    }
}