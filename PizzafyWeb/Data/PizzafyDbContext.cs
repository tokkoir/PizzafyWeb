using Microsoft.EntityFrameworkCore;
using PizzafyWeb.Models;

namespace PizzafyWeb.Data
{
    public class PizzafyDbContext : DbContext
    {
        public PizzafyDbContext(DbContextOptions<PizzafyDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Size> Sizes { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<MenuPrice> MenuPrices { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<Status> Statuses { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.HasIndex(e => e.Username).IsUnique();
                
                entity.Property(e => e.UserType)
                    .HasConversion(
                        v => v.ToString().ToLower(),
                        v => (UserType)Enum.Parse(typeof(UserType), v, true));
            });

            // Configure Category entity
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.CategoryId);
                entity.HasIndex(e => e.CategoryName).IsUnique();
            });

            // Configure Size entity
            modelBuilder.Entity<Size>(entity =>
            {
                entity.HasKey(e => e.SizeId);
                entity.HasIndex(e => new { e.CategoryId, e.SizeName }).IsUnique();
                
                entity.HasOne(e => e.Category)
                    .WithMany(c => c.Sizes)
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure MenuItem entity
            modelBuilder.Entity<MenuItem>(entity =>
            {
                entity.HasKey(e => e.MenuItemId);
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Category)
                    .WithMany(c => c.MenuItems)
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure MenuPrice entity
            modelBuilder.Entity<MenuPrice>(entity =>
            {
                entity.HasKey(e => e.PriceId);
                
                entity.HasIndex(e => new { e.MenuItemId, e.SizeId }).IsUnique();

                entity.HasOne(e => e.MenuItem)
                    .WithMany(m => m.MenuPrices)
                    .HasForeignKey(e => e.MenuItemId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Size)
                    .WithMany(s => s.MenuPrices)
                    .HasForeignKey(e => e.SizeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.UnitPrice)
                    .HasColumnType("decimal(10,2)");
            });

            // Configure Cart entity
            modelBuilder.Entity<Cart>(entity =>
            {
                entity.HasKey(e => e.CartId);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(10,2)");

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.MenuPrice)
                      .WithMany()
                      .HasForeignKey(e => e.PriceId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Status entity
            modelBuilder.Entity<Status>(entity =>
            {
                entity.HasKey(e => e.StatusId);
                entity.HasIndex(e => e.StatusName).IsUnique();
            });

            // Configure Order entity
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.OrderId);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(10,2)");
                entity.Property(e => e.DeliveryFee).HasColumnType("decimal(10,2)");

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Status)
                    .WithMany(s => s.Orders)
                    .HasForeignKey(e => e.StatusId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ModifiedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.ModifiedBy)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure OrderItem entity
            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(e => e.OrderItemId);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Subtotal).HasColumnType("decimal(10,2)").HasColumnName("line_total");

                entity.ToTable("order_detail");

                entity.HasOne(e => e.Order)
                    .WithMany(o => o.OrderItems)
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.MenuPrice)
                    .WithMany()
                    .HasForeignKey(e => e.PriceId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}