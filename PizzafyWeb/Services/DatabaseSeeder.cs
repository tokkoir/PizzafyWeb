using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Data;
using PizzafyWeb.Models;
using System.Security.Cryptography;
using System.Text;
using System.Data;

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
                // Ensure database exists (no migrations dependency)
                await _context.Database.EnsureCreatedAsync();

                // Align schema changes that might not be migrated yet
                await EnsureUserSchemaAsync();
                await EnsureOrderDetailSchemaAsync();
                await EnsurePaymentSchemaAsync();
                await EnsureUserAddressesSchemaAsync();
                await EnsureBarangaysSchemaAsync();
                await EnsureOrdersExtendedSchemaAsync();
                await EnsureMenuPriceSchemaAsync();
                // Removed EnsureProductLogsSchemaAsync to prevent (re)creating product_log table and triggers

                // Cleanup obsolete/duplicate legacy tables
                await CleanupObsoleteTablesAsync();

                // Seed barangays (Cebu City)
                if (!await _context.Barangays.AnyAsync())
                {
                    var names = new string[]
                    {
                        "Adlaon","Agsungot","Apas","Babag","Bacayan","Banilad","Basak Pardo","Basak San Nicolas","Binaliw","Bonbon","Budlaan","Buhisan","Bulacao","Buot-Taup Pardo","Busay","Calamba","Cambinocot","Capitol Site","Carreta","Cogon Pardo","Cogon Ramos","Day-as","Duljo Fatima","Ermita","Guadalupe","Guba","Hipodromo","Inayawan","Kalubihan","Kamagayan","Kasambagan","Kinasang-an Pardo","Labangon","Lahug","Lorega San Miguel","Lusaran","Mabini","Mabolo","Malubog","Mambaling","Pahina Central","Pahina San Nicolas","Pamutan","Pari-an","Paril","Pasil","Pit-os","Pulangbato","Pung-ol Sibugay","Pardo","Quiot Pardo","Sambag I","Sambag II","San Antonio","San Jose","San Nicolas Proper","San Roque","Santa Cruz","Santo Niño","Sawang Calero","Sinsin","Sirao","Suba","Sudlon I","Sudlon II","T. Padilla","Talamban","Taptap","Tejero","Tinago","Tisa","To-ong","Zapatera"
                    };
                    _context.Barangays.AddRange(names.Select(n => new Barangay { Name = n, City = "Cebu City" }));
                    await _context.SaveChangesAsync();
                }

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

                // Seed Statuses
                if (!await _context.Statuses.AnyAsync())
                {
                    var statuses = new List<Status>
                    {
                        new Status { StatusName = "Pending", Description = "Order has been placed and is awaiting preparation" },
                        new Status { StatusName = "Preparing", Description = "Order is currently being prepared" },
                        new Status { StatusName = "Out for Delivery", Description = "Rider is delivering the order" },
                        new Status { StatusName = "Ready for Pickup", Description = "Order is ready for pickup" },
                        new Status { StatusName = "Completed", Description = "Order has been completed" },
                        new Status { StatusName = "Cancelled", Description = "Order has been cancelled" }
                    };

                    _context.Statuses.AddRange(statuses);
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
                        
                        // Beverages sizes (use oz labels)
                        new Size { CategoryId = beveragesCategory.CategoryId, SizeName = "8oz" },
                        new Size { CategoryId = beveragesCategory.CategoryId, SizeName = "12oz" },
                        new Size { CategoryId = beveragesCategory.CategoryId, SizeName = "16oz" }
                    };

                    _context.Sizes.AddRange(sizes);
                    await _context.SaveChangesAsync();
                }

                // Normalize existing beverage sizes to oz labels if DB already had Small/Medium/Large with oz
                await NormalizeBeverageSizeNamesAsync();

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
                            Password = HashPassword("admin123"),
                            UserType = UserType.Admin,
                            AccountStatus = AccountStatus.Active,
                            CreatedAt = DateTime.Now,
                            PhoneNumber = "123-456-7890",
                            Address = "Admin Office, Company Building"
                        },
                        new User
                        {
                            Username = "customer1",
                            FirstName = "John",
                            LastName = "Doe",
                            Password = HashPassword("customer123"),
                            UserType = UserType.Customer,
                            AccountStatus = AccountStatus.Active,
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
                        new MenuItem { ItemName = "Coca Cola", Description = "Classic refreshing cola drink", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Sprite", Description = "Lemon-lime flavored soft drink", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Orange Juice", Description = "Fresh squeezed orange juice", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Iced Tea", Description = "Refreshing iced tea with lemon", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Water", Description = "Pure drinking water", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        
                        // Duplicate Sprite examples
                        new MenuItem { ItemName = "Sprite", Description = "Lemon-lime flavored soft drink", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" },
                        new MenuItem { ItemName = "Sprite", Description = "Lemon-lime flavored soft drink", CategoryId = beveragesCategory.CategoryId, UserId = adminUser.UserId, Image = "" }
                    };

                    _context.MenuItems.AddRange(menuItems);
                    await _context.SaveChangesAsync();

                    // Add multiple prices for each item (different sizes)
                    var menuPrices = new List<MenuPrice>();
                    
                    var sizesList = await _context.Sizes.ToListAsync();
                    var pizzaSizes = sizesList.Where(s => s.CategoryId == _context.Categories.First(c => c.CategoryName == "Pizza").CategoryId).ToList();
                    var sidesSizes = sizesList.Where(s => s.CategoryId == _context.Categories.First(c => c.CategoryName == "Sides").CategoryId).ToList();
                    var beverageSizes = sizesList.Where(s => s.CategoryId == _context.Categories.First(c => c.CategoryName == "Beverages").CategoryId).ToList();

                    var pizzaItems = menuItems.Where(m => m.CategoryId == _context.Categories.First(c => c.CategoryName == "Pizza").CategoryId).ToList();
                    for (int i = 0; i < pizzaItems.Count; i++)
                    {
                        var basePrice = new decimal[] { 12.99m, 14.99m, 15.99m, 17.99m, 16.99m, 13.99m, 15.99m, 14.99m }[i % 8];
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
                            menuPrices.Add(new MenuPrice { MenuItemId = pizzaItems[i].MenuItemId, SizeId = size.SizeId, UnitPrice = Math.Round(basePrice * sizeMultiplier, 2), IsAvailable = true });
                        }
                    }

                    var sidesItems = menuItems.Where(m => m.CategoryId == _context.Categories.First(c => c.CategoryName == "Sides").CategoryId).ToList();
                    for (int i = 0; i < sidesItems.Count; i++)
                    {
                        var basePrice = new decimal[] { 5.99m, 8.99m, 7.99m, 6.99m, 5.99m, 6.99m }[i % 6];
                        foreach (var size in sidesSizes)
                        {
                            var sizeMultiplier = size.SizeName switch
                            {
                                "Small" => 0.8m,
                                "Medium" => 1.0m,
                                "Large" => 1.4m,
                                _ => 1.0m
                            };
                            menuPrices.Add(new MenuPrice { MenuItemId = sidesItems[i].MenuItemId, SizeId = size.SizeId, UnitPrice = Math.Round(basePrice * sizeMultiplier, 2), IsAvailable = true });
                        }
                    }

                    var beverageItems = menuItems.Where(m => m.CategoryId == _context.Categories.First(c => c.CategoryName == "Beverages").CategoryId).ToList();
                    var regularBeverages = beverageItems.Take(5).ToList();
                    for (int i = 0; i < regularBeverages.Count; i++)
                    {
                        var basePrice = new decimal[] { 2.99m, 2.99m, 3.99m, 2.49m, 1.99m }[i % 5];
                        foreach (var size in beverageSizes)
                        {
                            var sizeMultiplier = size.SizeName switch
                            {
                                "8oz" => 0.8m,
                                "12oz" => 1.0m,
                                "16oz" => 1.3m,
                                _ => 1.0m
                            };
                            menuPrices.Add(new MenuPrice { MenuItemId = regularBeverages[i].MenuItemId, SizeId = size.SizeId, UnitPrice = Math.Round(basePrice * sizeMultiplier, 2), IsAvailable = true });
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

        private async Task EnsureProductLogsSchemaAsync()
        {
            try
            {
                await _context.Database.OpenConnectionAsync();
                using var cmd = _context.Database.GetDbConnection().CreateCommand();

                // Create product_log table if missing
                cmd.CommandText = @"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'product_log'";
                var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                if (!exists)
                {
                    cmd.CommandText = @"CREATE TABLE product_log (
                        log_id INT AUTO_INCREMENT PRIMARY KEY,
                        menu_item_id INT NULL,
                        item_name VARCHAR(100) NULL,
                        action VARCHAR(20) NOT NULL,
                        changed_by VARCHAR(50) NULL,
                        changed_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        details TEXT NULL,
                        INDEX idx_item_id (menu_item_id),
                        INDEX idx_action (action),
                        INDEX idx_changed_at (changed_at)
                    ) ENGINE=InnoDB";
                    await cmd.ExecuteNonQueryAsync();
                }

                // Helper to drop trigger if exists then create
                async Task CreateTriggerAsync(string name, string body)
                {
                    try
                    {
                        cmd.CommandText = $"DROP TRIGGER IF EXISTS `{name}`";
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch { }
                    cmd.CommandText = body;
                    await cmd.ExecuteNonQueryAsync();
                }

                // Trigger: log on new product (menu_item insert)
                await CreateTriggerAsync(
                    "trg_menu_item_insert_log",
                    @"CREATE TRIGGER trg_menu_item_insert_log
AFTER INSERT ON menu_item FOR EACH ROW
BEGIN
  DECLARE v_username VARCHAR(50);
  SELECT username INTO v_username FROM `user` WHERE user_id = NEW.user_id LIMIT 1;
  INSERT INTO product_log(menu_item_id, item_name, action, changed_by, details)
  VALUES (NEW.menu_item_id, NEW.item_name, 'created', COALESCE(@app_username, v_username, 'unknown'),
          CONCAT('Created item: ', NEW.item_name, IFNULL(CONCAT(' - ', NEW.description), '')));
END");

                // Trigger: log when menu_item updated
                await CreateTriggerAsync(
                    "trg_menu_item_update_log",
                    @"CREATE TRIGGER trg_menu_item_update_log
AFTER UPDATE ON menu_item FOR EACH ROW
BEGIN
  DECLARE v_username VARCHAR(50);
  DECLARE v_details TEXT;
  SET v_details = '';
  IF (NEW.item_name <=> OLD.item_name) = 0 THEN
    SET v_details = CONCAT_WS('; ', v_details, CONCAT('name: ', OLD.item_name, ' -> ', NEW.item_name));
  END IF;
  IF (NEW.description <=> OLD.description) = 0 THEN
    SET v_details = CONCAT_WS('; ', v_details, CONCAT('description changed'));
  END IF;
  IF (NEW.category_id <> OLD.category_id) THEN
    SET v_details = CONCAT_WS('; ', v_details, CONCAT('category_id: ', OLD.category_id, ' -> ', NEW.category_id));
  END IF;
  IF (NEW.image <=> OLD.image) = 0 THEN
    SET v_details = CONCAT_WS('; ', v_details, 'image updated');
  END IF;
  SELECT username INTO v_username FROM `user` WHERE user_id = NEW.user_id LIMIT 1;
  INSERT INTO product_log(menu_item_id, item_name, action, changed_by, details)
  VALUES (NEW.menu_item_id, NEW.item_name, 'updated', COALESCE(@app_username, v_username, 'unknown'), NULLIF(v_details,''));
END");

                // Trigger: log enable/disable and price change on menu_price update
                await CreateTriggerAsync(
                    "trg_menu_price_update_log",
                    @"CREATE TRIGGER trg_menu_price_update_log
AFTER UPDATE ON menu_price FOR EACH ROW
BEGIN
  DECLARE v_item_name VARCHAR(100);
  DECLARE v_size_name VARCHAR(50);
  DECLARE v_menu_item_id INT;
  SELECT mi.item_name, s.size_name, mi.menu_item_id INTO v_item_name, v_size_name, v_menu_item_id
  FROM menu_price mp
  JOIN menu_item mi ON mi.menu_item_id = mp.menu_item_id
  JOIN size s ON s.size_id = mp.size_id
  WHERE mp.price_id = NEW.price_id;

  IF (NEW.is_available <> OLD.is_available) THEN
    INSERT INTO product_log(menu_item_id, item_name, action, changed_by, details)
    VALUES (v_menu_item_id, v_item_name,
            IF(NEW.is_available = 1, 'enabled', 'disabled'),
            COALESCE(@app_username, 'unknown'),
            CONCAT('Size ', v_size_name, ' ', IF(NEW.is_available=1,'enabled','disabled')));
  END IF;

  IF (NEW.unit_price <> OLD.unit_price) THEN
    INSERT INTO product_log(menu_item_id, item_name, action, changed_by, details)
    VALUES (v_menu_item_id, v_item_name,
            'updated',
            COALESCE(@app_username, 'unknown'),
            CONCAT('Price for size ', v_size_name, ': ', OLD.unit_price, ' -> ', NEW.unit_price));
  END IF;
END");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed ensuring product logs schema: {ex.Message}");
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        private async Task CleanupObsoleteTablesAsync()
        {
            try
            {
                await _context.Database.OpenConnectionAsync();
                using var cmd = _context.Database.GetDbConnection().CreateCommand();

                async Task<bool> TableExists(string table)
                {
                    cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = '{table}'";
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                }

                async Task<bool> ColumnExists(string table, string column)
                {
                    cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = '{table}' AND column_name = '{column}'";
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                }

                async Task Exec(string sql)
                {
                    cmd.CommandText = sql;
                    try { await cmd.ExecuteNonQueryAsync(); } catch { }
                }

                async Task DropIfExists(string table)
                {
                    if (await TableExists(table))
                    {
                        await Exec($"DROP TABLE `{table}`");
                    }
                }

                // Drop user_tokens table entirely
                await DropIfExists("user_tokens");

                // Remove email-related columns from user if present
                if (await ColumnExists("user", "email"))
                {
                    await Exec("ALTER TABLE `user` DROP COLUMN `email`");
                }
                if (await ColumnExists("user", "email_verified"))
                {
                    await Exec("ALTER TABLE `user` DROP COLUMN `email_verified`");
                }
                if (await ColumnExists("user", "subscribed_to_emails"))
                {
                    await Exec("ALTER TABLE `user` DROP COLUMN `subscribed_to_emails`");
                }

                // Only drop legacy/duplicate tables if canonical ones are present
                if (await TableExists("order_detail"))
                {
                    await DropIfExists("order_items");
                    await DropIfExists("order_item");
                }
                if (await TableExists("payment"))
                {
                    await DropIfExists("payments");
                }
                if (await TableExists("menu_price"))
                {
                    await DropIfExists("menu_prices");
                }
                if (await TableExists("menu_item"))
                {
                    await DropIfExists("menu_items");
                }
                if (await TableExists("category"))
                {
                    await DropIfExists("categories");
                }
                if (await TableExists("size"))
                {
                    await DropIfExists("sizes");
                }
                if (await TableExists("user"))
                {
                    await DropIfExists("users");
                }
                if (await TableExists("orders"))
                {
                    await DropIfExists("order");
                }
                if (await TableExists("user_addresses"))
                {
                    await DropIfExists("user_address");
                    await DropIfExists("addresses");
                    await DropIfExists("address");
                }
                if (await TableExists("barangays"))
                {
                    await DropIfExists("barangay");
                }

                // New: ensure product_log table and its triggers are removed
                try { await Exec("DROP TRIGGER IF EXISTS `trg_menu_item_insert_log`"); } catch { }
                try { await Exec("DROP TRIGGER IF EXISTS `trg_menu_item_update_log`"); } catch { }
                try { await Exec("DROP TRIGGER IF EXISTS `trg_menu_price_update_log`"); } catch { }
                await DropIfExists("product_log");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Schema cleanup failed: {ex.Message}");
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        private async Task NormalizeBeverageSizeNamesAsync()
        {
            try
            {
                var beverages = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryName == "Beverages");
                if (beverages == null) return;

                // Load all beverage sizes
                var sizes = await _context.Sizes.Where(s => s.CategoryId == beverages.CategoryId).ToListAsync();
                if (sizes.Count == 0) return;

                string[] old8 = new[] { "Small (8 oz)", "Small (8oz)", "8 oz" };
                string[] old12 = new[] { "Medium (12 oz)", "Medium (12oz)", "12 oz" };
                string[] old16 = new[] { "Large (16 oz)", "Large (16oz)", "16 oz" };

                // Ensure target rows exist or determine target ids
                var target8 = sizes.FirstOrDefault(s => s.SizeName.Equals("8oz", StringComparison.OrdinalIgnoreCase));
                var target12 = sizes.FirstOrDefault(s => s.SizeName.Equals("12oz", StringComparison.OrdinalIgnoreCase));
                var target16 = sizes.FirstOrDefault(s => s.SizeName.Equals("16oz", StringComparison.OrdinalIgnoreCase));

                // Helper to merge old into target
                async Task<Size?> MergeOrRenameAsync(string[] olds, string newName, Size? target)
                {
                    var old = sizes.FirstOrDefault(s => olds.Any(o => s.SizeName.Equals(o, StringComparison.OrdinalIgnoreCase)));
                    if (old == null) return target; // nothing to do

                    if (target == null)
                    {
                        // No target exists yet: just rename old row
                        old.SizeName = newName;
                        await _context.SaveChangesAsync();
                        return old;
                    }
                    else if (old.SizeId != target.SizeId)
                    {
                        // Move references and delete old
                        var pricesToMove = await _context.MenuPrices.Where(mp => mp.SizeId == old.SizeId).ToListAsync();
                        foreach (var p in pricesToMove) p.SizeId = target.SizeId;
                        await _context.SaveChangesAsync();
                        _context.Sizes.Remove(old);
                        await _context.SaveChangesAsync();
                        return target;
                    }
                    return target;
                }

                target8 = await MergeOrRenameAsync(old8, "8oz", target8);
                target12 = await MergeOrRenameAsync(old12, "12oz", target12);
                target16 = await MergeOrRenameAsync(old16, "16oz", target16);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to normalize beverage size names: {ex.Message}");
            }
        }

        private async Task EnsureOrdersExtendedSchemaAsync()
        {
            try
            {
                await _context.Database.OpenConnectionAsync();
                using var cmd = _context.Database.GetDbConnection().CreateCommand();

                // Helper local function
                async Task<bool> ColumnExists(string table, string column)
                {
                    cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = '{table}' AND column_name = '{column}'";
                    return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                }

                // Add columns if missing
                async Task EnsureColumn(string name, string ddl)
                {
                    if (!await ColumnExists("orders", name))
                    {
                        cmd.CommandText = $"ALTER TABLE orders ADD COLUMN {ddl}";
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await EnsureColumn("order_type", "order_type ENUM('delivery','pickup') NOT NULL DEFAULT 'delivery' AFTER payment_id");
                await EnsureColumn("pickup_date", "pickup_date DATE NULL AFTER order_type");
                await EnsureColumn("pickup_time", "pickup_time TIME NULL AFTER pickup_date");
                await EnsureColumn("address_id", "address_id INT NULL AFTER pickup_time");
                await EnsureColumn("delivery_street", "delivery_street VARCHAR(255) NULL AFTER delivery_address");
                await EnsureColumn("delivery_barangay", "delivery_barangay VARCHAR(100) NULL AFTER delivery_street");
                await EnsureColumn("delivery_city", "delivery_city VARCHAR(50) NULL DEFAULT 'Cebu City' AFTER delivery_barangay");
                await EnsureColumn("delivery_landmark", "delivery_landmark VARCHAR(255) NULL AFTER delivery_city");
                await EnsureColumn("special_instructions", "special_instructions VARCHAR(250) NULL AFTER delivery_landmark");
                await EnsureColumn("payment_method", "payment_method ENUM('cash','gcash') NOT NULL DEFAULT 'cash' AFTER special_instructions");

                // Indexes and FK
                try { cmd.CommandText = "CREATE INDEX idx_address_id ON orders(address_id)"; await cmd.ExecuteNonQueryAsync(); } catch { }
                try { cmd.CommandText = "CREATE INDEX idx_order_type ON orders(order_type)"; await cmd.ExecuteNonQueryAsync(); } catch { }
                try { cmd.CommandText = "CREATE INDEX idx_payment_method ON orders(payment_method)"; await cmd.ExecuteNonQueryAsync(); } catch { }

                // FK to user_addresses
                try { cmd.CommandText = "ALTER TABLE orders ADD CONSTRAINT FK_orders_user_addresses_address_id FOREIGN KEY (address_id) REFERENCES user_addresses(id) ON DELETE SET NULL"; await cmd.ExecuteNonQueryAsync(); } catch { }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Schema alignment for orders extended fields failed: {ex.Message}");
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        private async Task EnsureMenuPriceSchemaAsync()
        {
            try
            {
                await _context.Database.OpenConnectionAsync();
                using var cmd = _context.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'menu_price' AND column_name = 'is_available'";
                var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                if (!exists)
                {
                    cmd.CommandText = "ALTER TABLE menu_price ADD COLUMN is_available TINYINT(1) NOT NULL DEFAULT 1 AFTER unit_price";
                    await cmd.ExecuteNonQueryAsync();
                    // Default all existing prices to available
                    cmd.CommandText = "UPDATE menu_price SET is_available = 1 WHERE is_available IS NULL";
                    try { await cmd.ExecuteNonQueryAsync(); } catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Schema alignment for menu_price failed: {ex.Message}");
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        private async Task EnsureUserAddressesSchemaAsync()
        {
            try
            {
                await _context.Database.OpenConnectionAsync();
                using var cmd = _context.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'user_addresses'";
                var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                if (!exists)
                {
                    cmd.CommandText = @"CREATE TABLE user_addresses (
                        id INT PRIMARY KEY AUTO_INCREMENT,
                        user_id INT NOT NULL,
                        street_address VARCHAR(255) NOT NULL,
                        barangay VARCHAR(100) NOT NULL,
                        city VARCHAR(50) NOT NULL DEFAULT 'Cebu City',
                        landmark VARCHAR(255) NULL,
                        is_default BOOLEAN DEFAULT FALSE,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                        FOREIGN KEY (user_id) REFERENCES user(user_id) ON DELETE CASCADE,
                        INDEX idx_user_id (user_id),
                        INDEX idx_is_default (is_default)
                    ) ENGINE=InnoDB";
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch { }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        private async Task EnsureBarangaysSchemaAsync()
        {
            try
            {
                await _context.Database.OpenConnectionAsync();
                using var cmd = _context.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'barangays'";
                var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                if (!exists)
                {
                    cmd.CommandText = @"CREATE TABLE barangays (
                        id INT PRIMARY KEY AUTO_INCREMENT,
                        name VARCHAR(100) NOT NULL,
                        city VARCHAR(50) NOT NULL DEFAULT 'Cebu City',
                        INDEX idx_city (city)
                    ) ENGINE=InnoDB";
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch { }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        private async Task EnsurePaymentSchemaAsync()
        {
            try
            {
                await _context.Database.OpenConnectionAsync();
                using var cmd = _context.Database.GetDbConnection().CreateCommand();

                // Ensure payment table exists
                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'payment'";
                var paymentTableExists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                if (!paymentTableExists)
                {
                    cmd.CommandText = @"CREATE TABLE payment (
                        payment_id INT AUTO_INCREMENT PRIMARY KEY,
                        payment_name VARCHAR(50) NOT NULL
                    ) ENGINE=InnoDB";
                    await cmd.ExecuteNonQueryAsync();
                }

                // Seed payment methods if empty
                cmd.CommandText = "SELECT COUNT(*) FROM payment";
                var paymentCount = 0;
                try { paymentCount = Convert.ToInt32(await cmd.ExecuteScalarAsync()); } catch { paymentCount = 0; }
                if (paymentCount == 0)
                {
                    cmd.CommandText = "INSERT INTO payment (payment_name) VALUES ('Cash on Delivery'), ('GCash')";
                    await cmd.ExecuteNonQueryAsync();
                }

                // Ensure orders.payment_id column exists
                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'orders' AND column_name = 'payment_id'";
                var paymentIdColExists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                if (!paymentIdColExists)
                {
                    cmd.CommandText = "ALTER TABLE orders ADD COLUMN payment_id INT NOT NULL DEFAULT 1";
                    await cmd.ExecuteNonQueryAsync();

                    // Create index
                    cmd.CommandText = "CREATE INDEX IX_orders_payment_id ON orders(payment_id)";
                    try { await cmd.ExecuteNonQueryAsync(); } catch { /* ignore */ }

                    // Add FK
                    cmd.CommandText = "ALTER TABLE orders ADD CONSTRAINT FK_orders_payment_payment_id FOREIGN KEY (payment_id) REFERENCES payment(payment_id) ON DELETE RESTRICT";
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Schema alignment for payment failed: {ex.Message}");
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        private async Task EnsureOrderDetailSchemaAsync()
        {
            try
            {
                await _context.Database.OpenConnectionAsync();
                using var cmd = _context.Database.GetDbConnection().CreateCommand();

                // Check existence
                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'order_detail'";
                var detailExists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;

                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'order_items'";
                var itemsExists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;

                if (!detailExists && itemsExists)
                {
                    // Rename table and columns to match requested schema
                    cmd.CommandText = "RENAME TABLE order_items TO order_detail";
                    await cmd.ExecuteNonQueryAsync();

                    cmd.CommandText = "ALTER TABLE order_detail CHANGE COLUMN order_item_id order_detail_id INT NOT NULL AUTO_INCREMENT";
                    await cmd.ExecuteNonQueryAsync();

                    cmd.CommandText = "ALTER TABLE order_detail CHANGE COLUMN subtotal line_total DECIMAL(10,2) NOT NULL";
                    await cmd.ExecuteNonQueryAsync();
                }
                else if (!detailExists && !itemsExists)
                {
                    // Fresh create using requested DDL
                    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS order_detail (
                        order_detail_id INT AUTO_INCREMENT PRIMARY KEY,
                        order_id INT NOT NULL,
                        price_id INT NOT NULL,
                        quantity INT NOT NULL,
                        unit_price DECIMAL(10,2) NOT NULL,
                        line_total DECIMAL(10,2) NOT NULL,
                        FOREIGN KEY (order_id) REFERENCES orders(order_id) ON DELETE CASCADE,
                        FOREIGN KEY (price_id) REFERENCES menu_price(price_id)
                    ) ENGINE=InnoDB";
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Schema alignment for order_detail failed: {ex.Message}");
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
            }
        }

        private async Task EnsureUserSchemaAsync()
        {
            try
            {
                await _context.Database.OpenConnectionAsync();
                using var cmd = _context.Database.GetDbConnection().CreateCommand();

                // Ensure account_status column exists on user table
                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'user' AND column_name = 'account_status'";
                var hasAccountStatus = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                if (!hasAccountStatus)
                {
                    cmd.CommandText = "ALTER TABLE `user` ADD COLUMN account_status VARCHAR(20) NOT NULL DEFAULT 'pending'";
                    await cmd.ExecuteNonQueryAsync();
                    cmd.CommandText = "UPDATE `user` SET account_status = 'active'";
                    try { await cmd.ExecuteNonQueryAsync(); } catch { }
                }
                try
                {
                    cmd.CommandText = "ALTER TABLE `user` MODIFY COLUMN account_status VARCHAR(20) NOT NULL DEFAULT 'active'";
                    await cmd.ExecuteNonQueryAsync();
                }
                catch { }

                cmd.CommandText = "UPDATE `user` SET account_status = 'active' WHERE LOWER(user_type) = 'customer' AND LOWER(account_status) = 'pending'";
                try { await cmd.ExecuteNonQueryAsync(); } catch { }

                // Remove obsolete email-related columns if they still exist
                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'user' AND column_name = 'email'";
                var hasEmail = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                if (hasEmail)
                {
                    try
                    {
                        cmd.CommandText = "ALTER TABLE `user` DROP COLUMN `email`";
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch { }
                }

                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'user' AND column_name = 'email_verified'";
                var hasEmailVerified = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                if (hasEmailVerified)
                {
                    try
                    {
                        cmd.CommandText = "ALTER TABLE `user` DROP COLUMN `email_verified`";
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch { }
                }

                cmd.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'user' AND column_name = 'subscribed_to_emails'";
                var hasSubscribed = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                if (hasSubscribed)
                {
                    try
                    {
                        cmd.CommandText = "ALTER TABLE `user` DROP COLUMN `subscribed_to_emails`";
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Schema alignment for user failed: {ex.Message}");
            }
            finally
            {
                await _context.Database.CloseConnectionAsync();
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