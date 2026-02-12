// Background/DispatchWorker.cs
using DocumentDispatchService.Data;
using DocumentDispatchService.Models;
using Microsoft.EntityFrameworkCore;

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
            _logger.LogInformation("DispatchWorker started. Owner={OwnerId}", _ownerId);

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
                    _logger.LogError(ex, "Unhandled error in DispatchWorker loop.");
                }

                var pollSeconds = GetInt("DispatchWorker:PollSeconds", 5);
                await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken);
            }

            _logger.LogInformation("DispatchWorker stopping. Owner={OwnerId}", _ownerId);
        }

        private async Task TickAsync(CancellationToken ct)
        {
            var batchSize = GetInt("DispatchWorker:BatchSize", 5);
            var leaseSeconds = GetInt("DispatchWorker:LeaseSeconds", 30);

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

            // Claim each job using an atomic update:
            // Only succeeds if it's still Pending and lease is still available.
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
                    _logger.LogInformation("CLAIMED dispatch {DispatchId} by {OwnerId}", id, _ownerId);
                }
            }

            if (claimed.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Claimed {Count} dispatch(es). Owner={OwnerId}", claimed.Count, _ownerId);

            // Process claimed jobs (still sequential for now; concurrency comes later in 5C)
            foreach (var id in claimed)
            {
                await ProcessOneAsync(id, ct);
            }
        }

        private async Task ProcessOneAsync(Guid dispatchId, CancellationToken ct)
        {
            var workSeconds = GetInt("DispatchWorker:WorkSeconds", 2);
            var leaseSeconds = GetInt("DispatchWorker:LeaseSeconds", 30);

            // Renew at 1/3 lease by default (configurable)
            var defaultRenew = Math.Max(1, leaseSeconds / 3);
            var renewEverySeconds = Math.Max(1, GetInt("DispatchWorker:LeaseRenewEverySeconds", defaultRenew));

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

            var now = DateTime.UtcNow;

            // Ensure we still own the lease and it's not expired
            var dispatch = await db.DispatchRequests.FirstOrDefaultAsync(
                x => x.Id == dispatchId &&
                     x.LockOwner == _ownerId &&
                     x.LockedUntilUtc != null &&
                     x.LockedUntilUtc >= now &&
                     x.Status == DispatchStatus.Processing,
                ct);

            if (dispatch is null)
            {
                _logger.LogWarning("Lost lease for dispatch {DispatchId}. Owner={OwnerId}", dispatchId, _ownerId);
                return;
            }

            using var logScope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["DispatchId"] = dispatchId,
                ["OwnerId"] = _ownerId
            });

            _logger.LogInformation("Processing dispatch.");

            using var renewCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var renewTask = Task.Run(
                () => LeaseRenewalLoopAsync(dispatchId, leaseSeconds, renewEverySeconds, renewCts.Token),
                CancellationToken.None);

            try
            {
                // Simulated work, but we keep it responsive to lease-loss.
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
                        // Renewal loop ended early: either lost lease or faulted.
                        await ObserveRenewalOutcomeAsync(renewTask);
                        _logger.LogWarning("Stopping processing because lease renewal ended.");
                        return;
                    }

                    elapsed += step;
                }

                var success = _random.NextDouble() > 0.2;

                if (success)
                {
                    dispatch.Status = DispatchStatus.Completed;
                    dispatch.LastError = null;
                }
                else
                {
                    dispatch.RetryCount++;

                    if (dispatch.RetryCount >= 3)
                    {
                        dispatch.Status = DispatchStatus.Failed;
                        dispatch.LastError = "Simulated delivery failure after 3 attempts.";
                    }
                    else
                    {
                        dispatch.Status = DispatchStatus.Pending;
                        dispatch.LastError = "Simulated transient failure.";
                    }
                }

                // Release the lease
                dispatch.LockOwner = null;
                dispatch.LockedUntilUtc = null;
                dispatch.UpdatedAtUtc = DateTime.UtcNow;

                await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Dispatch updated. Status={Status}, RetryCount={RetryCount}",
                    dispatch.Status,
                    dispatch.RetryCount);
            }
            finally
            {
                // Stop renewal loop
                renewCts.Cancel();

                try
                {
                    await renewTask;
                }
                catch
                {
                    // Observed via ObserveRenewalOutcomeAsync or logged inside loop.
                }
            }
        }

        private async Task LeaseRenewalLoopAsync(Guid dispatchId, int leaseSeconds, int renewEverySeconds, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(renewEverySeconds), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

                var ok = await TryRenewLeaseAsync(db, dispatchId, leaseSeconds, token);
                if (!ok)
                {
                    _logger.LogWarning("Lease renewal failed (lost lease).");
                    throw new InvalidOperationException("Lost lease during processing.");
                }

                _logger.LogDebug("Lease renewed.");
            }
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

        private static async Task ObserveRenewalOutcomeAsync(Task renewTask)
        {
            try
            {
                await renewTask;
            }
            catch
            {
                // Caller will log and stop processing.
            }
        }

        private int GetInt(string key, int defaultValue)
        {
            var value = _config[key];
            return int.TryParse(value, out var result) ? result : defaultValue;
        }
    }
}
