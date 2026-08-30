namespace ProductCatalog.DTOs
{
    public class ProductResponse
    {
        public long Id { get; set; }

        public string Name { get; set; } = null!;

        public decimal Price { get; set; }

        public int StockQuantity { get; set; }

        public string? Category { get; set; }
    }
}
