namespace DocumentDispatchService.Contracts
{
    public sealed class PagedResponse<T>
    {
        public int Skip { get; set; }
        public int Take { get; set; }
        public int Count { get; set; }
        public List<T> Items { get; set; } = [];
    }
}
