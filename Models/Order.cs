namespace ProductCatalog.Models
{
    public sealed class Order
    {
        public long Id { get; set; }

        public string CustomerEmail { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string Status { get; set; } = string.Empty;

        public string IdempotencyKey {  get; set; } = string.Empty;

        public List<OrderItem> Items { get; set; } = new();
    }
}
