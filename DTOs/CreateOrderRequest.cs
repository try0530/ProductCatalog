namespace ProductCatalog.DTOs
{
    public sealed class CreateOrderRequest
    {
        public string CustomerEmail { get; set; } = string.Empty;

        public List<CreateOrderItemRequest> Items { get; set; } = new();
    }

    public sealed class CreateOrderItemRequest
    {
        public long ProductId { get; set; }

        public int Quantity { get; set; }
    }
}
