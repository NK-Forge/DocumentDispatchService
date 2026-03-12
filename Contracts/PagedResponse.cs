namespace DocumentDispatchService.Contracts
{
    public sealed class PagedResponse<T>
    {
        public required int Skip { get; init; }
        public required int Take { get; init; }
        public required int Count { get; init; }
        public required int Total { get; init; }
        public required IReadOnlyList<T> Items { get; init; }
    }
}