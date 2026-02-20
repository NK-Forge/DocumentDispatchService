using DocumentDispatchService.Data;
using DocumentDispatchService.Models;
using DocumentDispatchService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DocumentDispatchService.Pages.Ops
{
    public sealed class IndexModel : PageModel
    {
        private readonly DispatchDbContext _db;
        private readonly OpsActivityLog _activity;

        public IndexModel(DispatchDbContext db, OpsActivityLog activity)
        {
            _db = db;
            _activity = activity;
        }

        // ------------------------
        // Snapshot + Recent
        // ------------------------

        public async Task<IActionResult> OnGetSnapshotAsync()
        {
            var now = DateTime.UtcNow;
            var staleThreshold = now.AddMinutes(-5);

            var snapshot = await _db.DispatchRequests
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    total = g.Count(),
                    pending = g.Count(d => d.Status == DispatchStatus.Pending),
                    processing = g.Count(d => d.Status == DispatchStatus.Processing),
                    completed = g.Count(d => d.Status == DispatchStatus.Completed),
                    failed = g.Count(d => d.Status == DispatchStatus.Failed),
                    locked = g.Count(d => d.LockedUntilUtc != null && d.LockedUntilUtc > now),
                    staleProcessing = g.Count(d =>
                        d.Status == DispatchStatus.Processing &&
                        d.UpdatedAtUtc < staleThreshold)
                })
                .SingleOrDefaultAsync();

            return new JsonResult(snapshot ?? new
            {
                total = 0,
                pending = 0,
                processing = 0,
                completed = 0,
                failed = 0,
                locked = 0,
                staleProcessing = 0
            });
        }

        public async Task<IActionResult> OnGetRecentAsync(int take = 15, bool showCompleted = false)
        {
            if (take < 1) take = 15;
            if (take > 100) take = 100;

            var query = _db.DispatchRequests.AsQueryable();

            if (!showCompleted)
            {
                query = query.Where(d => d.Status != DispatchStatus.Completed);
            }

            // Key change: sort by UpdatedAtUtc so active work bubbles up
            var data = await query
                .OrderByDescending(d => d.UpdatedAtUtc)
                .ThenByDescending(d => d.CreatedAtUtc)
                .Take(take)
                .Select(d => new
                {
                    id = d.Id,
                    status = d.Status,
                    recipientEmail = d.RecipientEmail,
                    documentName = d.DocumentName,
                    retryCount = d.RetryCount,
                    createdAtUtc = d.CreatedAtUtc,
                    updatedAtUtc = d.UpdatedAtUtc,
                    lockOwner = d.LockOwner,
                    lockedUntilUtc = d.LockedUntilUtc,
                    hasError = d.LastError != null
                })
                .ToListAsync();

            return new JsonResult(data);
        }

        // ------------------------
        // Activity Feed
        // ------------------------

        public IActionResult OnGetActivityAsync(int take = 20)
        {
            if (take < 1) take = 20;
            if (take > 100) take = 100;

            var events = _activity.GetLatest(take);
            return new JsonResult(events);
        }

        // ------------------------
        // Demonstration Handlers
        // ------------------------

        public async Task<IActionResult> OnPostCreateDemoAsync(int count, string mode = "live")
        {
            if (count < 1) count = 1;
            if (count > 50) count = 50;

            var now = DateTime.UtcNow;
            var rng = new Random();

            var items = new List<DispatchRequest>(count);

            for (var i = 0; i < count; i++)
            {
                DispatchStatus status;

                if (string.Equals(mode, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    status = DispatchStatus.Failed;
                }
                else if (string.Equals(mode, "mixed", StringComparison.OrdinalIgnoreCase))
                {
                    var roll = rng.Next(0, 100);
                    if (roll < 70) status = DispatchStatus.Completed;
                    else if (roll < 90) status = DispatchStatus.Processing;
                    else status = DispatchStatus.Failed;
                }
                else
                {
                    // live + pending => worker-driven pipeline
                    status = DispatchStatus.Pending;
                }

                var recipient = $"user{rng.Next(1000, 9999)}@test.com";
                var docName = $"Document-{rng.Next(100, 999)}.pdf";

                var createdAt = now.AddSeconds(-rng.Next(0, 900)); // last 15 minutes
                var updatedAt = createdAt;

                if (status == DispatchStatus.Processing)
                {
                    updatedAt = rng.Next(0, 100) < 40
                        ? now.AddMinutes(-rng.Next(11, 45))
                        : now.AddSeconds(-rng.Next(10, 300));
                }

                items.Add(new DispatchRequest
                {
                    Id = Guid.NewGuid(),
                    RecipientEmail = recipient,
                    DocumentName = docName,
                    Status = status,
                    RetryCount = status == DispatchStatus.Failed ? rng.Next(1, 4) : 0,
                    CreatedAtUtc = createdAt,
                    UpdatedAtUtc = updatedAt,
                    LockOwner = null,
                    LockedUntilUtc = null,
                    LastError = status == DispatchStatus.Failed ? "Demonstration: simulated failure." : null
                });
            }

            _db.DispatchRequests.AddRange(items);
            await _db.SaveChangesAsync();

            var label = string.Equals(mode, "live", StringComparison.OrdinalIgnoreCase)
                ? "Live Flow (Worker Driven)"
                : mode;

            _activity.Add("OPS", $"Created {count} demo job(s): {label}.");

            TempData["OpsMessage"] = $"Created {count} demonstration job(s): {label}.";
            return Redirect("/ops");
        }

        public async Task<IActionResult> OnPostClearDemoAsync(string? confirm)
        {
            if (!string.Equals(confirm, "CLEAR", StringComparison.OrdinalIgnoreCase))
            {
                TempData["OpsMessage"] = "Clear cancelled. Type CLEAR to confirm.";
                return Redirect("/ops");
            }

            await _db.DispatchRequests.ExecuteDeleteAsync();

            _activity.Add("OPS", "Cleared all jobs.");

            TempData["OpsMessage"] = "All jobs cleared.";
            return Redirect("/ops");
        }
    }
}
