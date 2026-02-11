namespace DocumentDispatchService.Models
{
    public sealed class DispatchRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string RecipientEmail { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;

        public DispatchStatus Status { get; set; } = DispatchStatus.Pending;

        public int RetryCount { get; set; } = 0;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public string? LastError { get; set; }
    }
}
