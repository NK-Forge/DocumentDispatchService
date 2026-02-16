namespace DocumentDispatchService.Contracts
{
    public sealed class DispatchResponse
    {
        public Guid Id { get; init; }
        public string RecipientEmail { get; init; } = string.Empty;
        public string DocumentName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public int RetryCount { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
        public string? LastError { get; init; }
    }
}
