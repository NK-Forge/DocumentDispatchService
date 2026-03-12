namespace DocumentDispatchService.Background;

public sealed class DispatchWorkerOptions
{
    public int PollDelayMs { get; set; } = 300;
    public int BatchSize { get; set; } = 5;
    public int MaxConcurrency { get; set; } = 2;
    public int LeaseSeconds { get; set; } = 30;
    public int LeaseRenewEverySeconds { get; set; } = 10;
    public int WorkDelayMs { get; set; } = 600;
    public int WorkSeconds { get; set; } = 2;
    public int MaxRetries { get; set; } = 3;
}