using Prometheus;

namespace DocumentDispatchService.Observability
{
    public static class DispatchMetrics
    {
        public static readonly Counter DispatchClaimedTotal = Metrics.CreateCounter(
            "dispatch_claimed_total",
            "Total number of dispatches claimed by workers.");

        public static readonly Counter DispatchProcessedTotal = Metrics.CreateCounter(
            "dispatch_processed_total",
            "Total number of dispatches processed by workers.",
            new CounterConfiguration
            {
                LabelNames = new[] { "result" } // completed, failed, requeued, lease_lost
            });

        public static readonly Counter DispatchLeaseRenewedTotal = Metrics.CreateCounter(
            "dispatch_lease_renewed_total",
            "Total number of dispatch lease renewals performed.");

        public static readonly Counter DispatchErrorsTotal = Metrics.CreateCounter(
            "dispatch_errors_total",
            "Total number of errors encountered by workers.",
            new CounterConfiguration
            {
                LabelNames = new[] { "stage" } //tick, process, renew
            });

        public static readonly Gauge DispatchInflight = Metrics.CreateGauge(
            "dispatch_inflight",
            "Number of dispatches currently processing in this instance.");

        public static readonly Histogram DispatchProcessingDurationSeconds = Metrics.CreateHistogram(
            "dispatch_processing_duration_seconds",
            "Time spent processing a dispatch.",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(start: 0.25, factor: 2, count: 10)
            });
    }
}
