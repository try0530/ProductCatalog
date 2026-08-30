namespace ProductCatalog.DTOs
{
    public class PagedResponse<T>
    {
        public IReadOnlyList<T> Items { get; set; } = [];

        public int Page {  get; set; }

        public int Size { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)Size);
    }
}
