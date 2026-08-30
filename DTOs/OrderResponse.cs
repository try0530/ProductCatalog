namespace ProductCatalog.DTOs
{
    public sealed class OrderResponse
    {
        public long Id { get; set; }

        public string CustomerEmail { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string Status { get; set; } = string.Empty;

        public List<OrderItemResponse> Items { get; set; } = new();
    }

    public sealed class OrderItemResponse
    {
        public long Id { get; set; }

        public long ProductId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }
    }
}
