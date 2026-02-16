using DocumentDispatchService.Data;
using DocumentDispatchService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DocumentDispatchService.Pages.Ops
{
    public sealed class DispatchModel : PageModel
    {
        private readonly DispatchDbContext _db;

        public DispatchModel(DispatchDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public Guid Id { get; set; }

        public DispatchRequest? Dispatch { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Dispatch = await _db.DispatchRequests.FirstOrDefaultAsync(d => d.Id == Id);
            return Page();
        }

        public async Task<IActionResult> OnPostRequeueAsync()
        {
            var now = DateTime.UtcNow;

            var entity = await _db.DispatchRequests.FirstOrDefaultAsync(d => d.Id == Id);
            if (entity is null)
            {
                TempData["OpsMessage"] = "Dispatch not found.";
                return Redirect("/ops");
            }

            entity.Status = DispatchStatus.Pending;
            entity.LockOwner = null;
            entity.LockedUntilUtc = null;
            entity.UpdatedAtUtc = now;

            await _db.SaveChangesAsync();

            TempData["OpsMessage"] = "Dispatch requeued to Pending and lock cleared.";
            return Redirect($"/ops/dispatch/{Id}");
        }

        public async Task<IActionResult> OnPostReleaseLockAsync()
        {
            var now = DateTime.UtcNow;

            var entity = await _db.DispatchRequests.FirstOrDefaultAsync(d => d.Id == Id);
            if (entity is null)
            {
                TempData["OpsMessage"] = "Dispatch not found.";
                return Redirect("/ops");
            }

            entity.LockOwner = null;
            entity.LockedUntilUtc = null;
            entity.UpdatedAtUtc = now;

            await _db.SaveChangesAsync();

            TempData["OpsMessage"] = "Lock released.";
            return Redirect($"/ops/dispatch/{Id}");
        }

        public async Task<IActionResult> OnPostClearErrorAsync()
        {
            var now = DateTime.UtcNow;

            var entity = await _db.DispatchRequests.FirstOrDefaultAsync(d => d.Id == Id);
            if (entity is null)
            {
                TempData["OpsMessage"] = "Dispatch not found.";
                return Redirect("/ops");
            }

            entity.LastError = null;
            entity.UpdatedAtUtc = now;

            await _db.SaveChangesAsync();

            TempData["OpsMessage"] = "LastError cleared.";
            return Redirect($"/ops/dispatch/{Id}");
        }

        public async Task<IActionResult> OnPostResetRetriesAsync()
        {
            var now = DateTime.UtcNow;

            var entity = await _db.DispatchRequests.FirstOrDefaultAsync(d => d.Id == Id);
            if (entity is null)
            {
                TempData["OpsMessage"] = "Dispatch not found.";
                return Redirect("/ops");
            }

            entity.RetryCount = 0;
            entity.UpdatedAtUtc = now;

            await _db.SaveChangesAsync();

            TempData["OpsMessage"] = "RetryCount reset to 0.";
            return Redirect($"/ops/dispatch/{Id}");
        }
    }
}
