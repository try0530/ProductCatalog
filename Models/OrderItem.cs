namespace ProductCatalog.Models
{
    public sealed class OrderItem
    {
        public long Id { get; set; }

        public long OrderId { get; set; }

        public long ProductId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public Order Order { get; set; } = null!;
    }
}
