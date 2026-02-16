using DocumentDispatchService.Data;
using DocumentDispatchService.Models;
using DocumentDispatchService.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static DocumentDispatchService.Observability.DispactchLogEvents;
using Prometheus;

namespace DocumentDispatchService.Background
{
    public sealed class DispatchWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DispatchWorker> _logger;
        private readonly IConfiguration _config;

        private readonly Random _random = new();
        private readonly string _ownerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

        public DispatchWorker(IServiceScopeFactory scopeFactory, ILogger<DispatchWorker> logger, IConfiguration config)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(WorkerStarted, "DispatchWorker started. Owner={OwnerId}", _ownerId);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TickAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // normal shutdown
                }
                catch (Exception ex)
                {
                    DispatchMetrics.DispatchErrorsTotal.WithLabels("tick").Inc();
                    _logger.LogError(WorkerLoopError, ex, "Unhandled error in DispatchWorker loop.");
                }

                var pollSeconds = GetInt("DispatchWorker:PollSeconds", 5);
                await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken);
            }

            _logger.LogInformation(WorkerStopping, "DispatchWorker stopping. Owner={OwnerId}", _ownerId);
        }

        private async Task TickAsync(CancellationToken ct)
        {
            var batchSize = GetInt("DispatchWorker:BatchSize", 5);
            var leaseSeconds = GetInt("DispatchWorker:LeaseSeconds", 30);
            var maxConcurrency = Math.Max(1, GetInt("DispatchWorker:MaxConcurrency", 2));

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

            var now = DateTime.UtcNow;
            var leaseUntil = now.AddSeconds(leaseSeconds);

            var candidates = await db.DispatchRequests
                .Where(x =>
                    x.Status == DispatchStatus.Pending &&
                    (x.LockedUntilUtc == null || x.LockedUntilUtc < now))
                .OrderBy(x => x.CreatedAtUtc)
                .Take(batchSize)
                .Select(x => x.Id)
                .ToListAsync(ct);

            if (candidates.Count == 0)
            {
                return;
            }

            var claimed = new List<Guid>();

            foreach (var id in candidates)
            {
                var rows = await db.DispatchRequests
                    .Where(x =>
                        x.Id == id &&
                        x.Status == DispatchStatus.Pending &&
                        (x.LockedUntilUtc == null || x.LockedUntilUtc < now))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.LockOwner, _ownerId)
                        .SetProperty(x => x.LockedUntilUtc, leaseUntil)
                        .SetProperty(x => x.Status, DispatchStatus.Processing)
                        .SetProperty(x => x.UpdatedAtUtc, now),
                        ct);

                if (rows == 1)
                {
                    claimed.Add(id);
                    DispatchMetrics.DispatchClaimedTotal.Inc();
                    _logger.LogInformation(DispatchClaimed, "Claimed dispatch {DispatchId} by {OwnerId}", id, _ownerId);
                }
            }

            if (claimed.Count == 0)
            {
                return;
            }

            _logger.LogInformation(
                DispatchBatchClaimed,
                "Batch claimed. Count={Count}  OwnerId={OwnerId} MaxConcurrency={MaxConcurrency}",
                claimed.Count,
                _ownerId,
                maxConcurrency);

            using var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var tasks = new List<Task>(claimed.Count);

            foreach (var id in claimed)
            {
                await gate.WaitAsync(ct);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await ProcessOneAsync(id, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        // shutdown
                    }
                    catch (Exception ex)
                    {
                        DispatchMetrics.DispatchErrorsTotal.WithLabels("process").Inc();
                        _logger.LogError(DispatchProcessError, ex, "Unhandled exception while processing dispatch {DispatchId}", id);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }, CancellationToken.None));
            }

            await Task.WhenAll(tasks);
        }

        private async Task ProcessOneAsync(Guid dispatchId, CancellationToken ct)
        {
            var workSeconds = GetInt("DispatchWorker:WorkSeconds", 2);
            var leaseSeconds = GetInt("DispatchWorker:LeaseSeconds", 30);

            var defaultRenew = Math.Max(1, leaseSeconds / 3);
            var renewEverySeconds = Math.Max(1, GetInt("DispatchWorker:LeaseRenewEverySeconds", defaultRenew));

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

            var now = DateTime.UtcNow;

            var dispatch = await db.DispatchRequests.FirstOrDefaultAsync(
                x => x.Id == dispatchId &&
                     x.LockOwner == _ownerId &&
                     x.LockedUntilUtc != null &&
                     x.LockedUntilUtc >= now &&
                     x.Status == DispatchStatus.Processing,
                ct);

            if (dispatch is null)
            {
                DispatchMetrics.DispatchProcessedTotal.WithLabels("lease_lost").Inc();
                _logger.LogWarning(LeaseLost, "Lost lease.  DispatchId={DispatchId} OwnerId={OwnerId}", dispatchId, _ownerId);
                return;
            }

            using var logScope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["DispatchId"] = dispatchId,
                ["OwnerId"] = _ownerId
            });

            DispatchMetrics.DispatchInflight.Inc();
            using var timer = DispatchMetrics.DispatchProcessingDurationSeconds.NewTimer();

            _logger.LogInformation(DispatchProcessingStart, "Processing started.");

            using var renewCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            // Lease renewal loop returns:
            // - true  => renewal OK (or cancelled because work finished)
            // - false => lost lease during work
            var renewTask = LeaseRenewalLoopAsync(dispatchId, leaseSeconds, renewEverySeconds, renewCts.Token);

            try
            {
                var total = TimeSpan.FromSeconds(workSeconds);
                var chunk = TimeSpan.FromSeconds(1);

                var elapsed = TimeSpan.Zero;
                while (elapsed < total)
                {
                    var remaining = total - elapsed;
                    var step = remaining < chunk ? remaining : chunk;

                    var completed = await Task.WhenAny(Task.Delay(step, ct), renewTask);
                    if (completed == renewTask)
                    {
                        var ok = await renewTask;
                        if (!ok)
                        {
                            DispatchMetrics.DispatchProcessedTotal.WithLabels("lease_lost").Inc();
                            _logger.LogWarning("Stopping processing because lease renewal reported lost lease.");
                            return;
                        }

                        // If renewal task ended "ok", it likely means cancellation happened.
                        // We should not stop processing in that case; but it should only cancel on shutdown.
                    }

                    elapsed += step;
                }

                var success = _random.NextDouble() > 0.2;

                if (success)
                {
                    dispatch.Status = DispatchStatus.Completed;
                    dispatch.LastError = null;
                    DispatchMetrics.DispatchProcessedTotal.WithLabels("completed").Inc();
                }
                else
                {
                    dispatch.RetryCount++;

                    if (dispatch.RetryCount >= 3)
                    {
                        dispatch.Status = DispatchStatus.Failed;
                        dispatch.LastError = "Simulated delivery failure after 3 attempts.";
                        DispatchMetrics.DispatchProcessedTotal.WithLabels("failed").Inc();
                    }
                    else
                    {
                        dispatch.Status = DispatchStatus.Pending;
                        dispatch.LastError = "Simulated transient failure.";
                        DispatchMetrics.DispatchProcessedTotal.WithLabels("requeued").Inc();
                    }
                }

                dispatch.LockOwner = null;
                dispatch.LockedUntilUtc = null;
                dispatch.UpdatedAtUtc = DateTime.UtcNow;

                await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    DispatchUpdated,
                    "Dispatch updated. Status={Status}, RetryCount={RetryCount}",
                    dispatch.Status,
                    dispatch.RetryCount);
            }
            finally
            {
                DispatchMetrics.DispatchInflight.Dec();

                renewCts.Cancel();

                try
                {
                    // Renew task should exit cleanly on cancel.
                    await renewTask;
                }
                catch
                {
                    // ignore
                }
            }
        }

        private async Task<bool> LeaseRenewalLoopAsync(Guid dispatchId, int leaseSeconds, int renewEverySeconds, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(renewEverySeconds), token);
                }
                catch (OperationCanceledException)
                {
                    return true;
                }

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

                var ok = await TryRenewLeaseAsync(db, dispatchId, leaseSeconds, token);
                if (!ok)
                {
                    DispatchMetrics.DispatchErrorsTotal.WithLabels("renew").Inc();
                    _logger.LogWarning(LeaseLost, "Lease renewal failed (lost lease).");
                    return false;
                }

                DispatchMetrics.DispatchLeaseRenewedTotal.Inc();
                _logger.LogDebug(LeaseRenewed, "Lease renewed.");
            }

            return true;
        }

        private async Task<bool> TryRenewLeaseAsync(DispatchDbContext db, Guid dispatchId, int leaseSeconds, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var newUntil = now.AddSeconds(leaseSeconds);

            var rows = await db.DispatchRequests
                .Where(x =>
                    x.Id == dispatchId &&
                    x.Status == DispatchStatus.Processing &&
                    x.LockOwner == _ownerId &&
                    x.LockedUntilUtc != null &&
                    x.LockedUntilUtc >= now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.LockedUntilUtc, newUntil)
                    .SetProperty(x => x.UpdatedAtUtc, now),
                    ct);

            return rows == 1;
        }

        private int GetInt(string key, int defaultValue)
        {
            var value = _config[key];
            return int.TryParse(value, out var result) ? result : defaultValue;
        }
    }
}
