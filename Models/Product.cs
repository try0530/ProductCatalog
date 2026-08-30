namespace ProductCatalog.Models
{
    public class Product
    {
        public long Id { get; set; }

        public string Name { get; set; } = null!;

        public decimal Price { get; set; }

        public int StockQuantity { get; set; }

        public string? Category { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
