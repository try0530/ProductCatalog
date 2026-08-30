using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyModel.Resolution;
using ProductCatalog.Models;

namespace ProductCatalog.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }

        public DbSet<Product> Products => Set<Product>();

        public DbSet<Order> Orders => Set<Order>();

        public DbSet<OrderItem> OrderItems => Set<OrderItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureProducts(modelBuilder);
            ConfigureOrders(modelBuilder);
            ConfigureOrderItems(modelBuilder);
        }

        private static void ConfigureProducts(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<Product>();
            entity.ToTable("products");

            entity.HasKey(product => product.Id);

            entity.Property(product => product.Id).HasColumnName("id").ValueGeneratedNever();

            entity.Property(product => product.Name).HasColumnName("name").HasMaxLength(255).IsRequired();

            entity.Property(product => product.Price).HasColumnName("price").HasColumnType("decimal(10, 2)").IsRequired();

            entity.Property(product => product.StockQuantity).HasColumnName("stock_quantity").IsRequired();

            entity.Property(product => product.Category).HasColumnName("category").HasMaxLength(100);

        }

        private static void ConfigureOrders(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<Order>();

            entity.ToTable("orders");

            entity.HasKey(order => order.Id);

            entity.Property(order => order.Id).HasColumnName("id").ValueGeneratedOnAdd();

            entity.Property(order => order.CustomerEmail).HasColumnName("customer_email").HasMaxLength(255).IsRequired();

            entity.Property(order => order.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.Property(order => order.Status).HasColumnName("status").HasMaxLength(50).IsRequired();

            entity.Property(order => order.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100).IsRequired();

            // UNIQUE
            // guarantee for idempotency
            entity.HasIndex(order => order.IdempotencyKey).IsUnique();

            entity.HasMany(order => order.Items).WithOne(item => item.Order).HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Cascade);
        }

        private static void ConfigureOrderItems(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<OrderItem>();

            entity.ToTable("order_items");

            entity.HasKey(item => item.Id);

            entity.Property(item => item.Id).HasColumnName("id").ValueGeneratedOnAdd();

            entity.Property(item => item.OrderId).HasColumnName("order_id").IsRequired();

            entity.Property(item => item.ProductId).HasColumnName("product_id").IsRequired();

            entity.Property(item => item.Quantity).HasColumnName("quatnity").IsRequired();

            entity.Property(item => item.UnitPrice).HasColumnName("unit_price").HasColumnType("decimal(10, 2)").IsRequired();

            entity.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(item => item.OrderId);

            entity.HasIndex(item => item.ProductId);
        }
    }
}
