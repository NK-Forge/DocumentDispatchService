namespace DocumentDispatchService.Observability
{
    public static class DispatchLogEvents
    {
        public static readonly EventId WorkerStarted = new(5100, nameof(WorkerStarted));
        public static readonly EventId WorkerStopping = new(5101, nameof(WorkerStopping));

        public static readonly EventId DispatchClaimed = new EventId(5200, nameof(DispatchClaimed));
        public static readonly EventId DispatchBatchClaimed = new EventId(5201, nameof(DispatchBatchClaimed));

        public static readonly EventId DispatchProcessingStart = new(5300, nameof(DispatchProcessingStart));
        public static readonly EventId DispatchProcessingStop = new(5301, nameof(DispatchProcessingStop));

        public static readonly EventId LeaseRenewed = new(5400, nameof(LeaseRenewed));
        public static readonly EventId LeaseLost = new(5401, nameof(LeaseLost));

        public static readonly EventId DispatchUpdated = new(5500, nameof(DispatchUpdated));

        public static readonly EventId WorkerLoopError = new(5900, nameof(WorkerLoopError));
        public static readonly EventId DispatchProcessError = new(5901, nameof(DispatchProcessError));

    }
}
