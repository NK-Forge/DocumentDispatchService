using DocumentDispatchService.Data;
using DocumentDispatchService.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentDispatchService.Background
{
    public sealed class DispatchWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DispatchWorker> _logger;
        private readonly Random _random = new();

        public DispatchWorker(IServiceScopeFactory scopeFactory, ILogger<DispatchWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DispatchWorker started.");

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

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }

            _logger.LogInformation("DispatchWorker stopping.");
        }

        private async Task TickAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

            var pending = await db.DispatchRequests
                .Where(x => x.Status == DispatchStatus.Pending)
                .OrderBy(x => x.CreatedAtUtc)
                .Take(5)
                .ToListAsync(ct);

            if (pending.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;

            foreach (var dispatch in pending)
            {
                dispatch.Status = DispatchStatus.Processing;
                dispatch.UpdatedAtUtc = now;
            }

            await db.SaveChangesAsync(ct);

            foreach (var dispatch in pending)
            {
                await ProcessOneAsync(dispatch.Id, ct);
            }
        }

        private async Task ProcessOneAsync(Guid dispatchId, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

            var dispatch = await db.DispatchRequests.FirstOrDefaultAsync(x => x.Id == dispatchId, ct);
            if (dispatch is null)
            {
                return;
            }

            _logger.LogInformation("Processing dispatch {DispatchId}", dispatch.Id);

            await Task.Delay(TimeSpan.FromSeconds(2), ct);

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

            dispatch.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Dispatch {DispatchId} updated. Status={Status}, RetryCount={RetryCount}",
                dispatch.Id,
                dispatch.Status,
                dispatch.RetryCount
            );
        }
    }
}
