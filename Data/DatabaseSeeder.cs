using ProductCatalog.Models;

namespace ProductCatalog.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        // make sure reset when run
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        db.Products.AddRange(
            new Product
            {
                Id = 1,
                Name = "Wireless Mouse",
                Price = 24.99m,
                StockQuantity = 50,
                Category = "Electronics"
            },
            new Product
            {
                Id = 2,
                Name = "Mechanical Keyboard",
                Price = 79.99m,
                StockQuantity = 10,
                Category = "Electronics"
            },
            new Product
            {
                Id = 3,
                Name = "Desk Lamp",
                Price = 34.50m,
                StockQuantity = 0,
                Category = "Home"
            },
            new Product
            {
                Id = 4,
                Name = "Standing Desk",
                Price = 249.00m,
                StockQuantity = 1,
                Category = "Home"
            },
            new Product
            {
                Id = 5,
                Name = "Notebook",
                Price = 4.99m,
                StockQuantity = 200,
                Category = "Office"
            }
        );

        await db.SaveChangesAsync();
    }
}