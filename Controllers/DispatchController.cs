using DocumentDispatchService.Contracts;
using DocumentDispatchService.Data;
using DocumentDispatchService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocumentDispatchService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class DispatchController : ControllerBase
    {
        private readonly DispatchDbContext _db;

        public DispatchController(DispatchDbContext db)
        {
            _db = db;
        }

        // POST: api/dispatch
        [HttpPost]
        public async Task<ActionResult<DispatchRequest>> Create([FromBody] CreateDispatchRequest request, CancellationToken ct)
        {

            var now = DateTime.UtcNow;

            var entity = new DispatchRequest
            {
                RecipientEmail = request.RecipientEmail.Trim(),
                DocumentName = request.DocumentName.Trim(),
                Status = DispatchStatus.Pending,
                RetryCount = 0,
                LastError = null,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _db.DispatchRequests.Add(entity);
            await _db.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        // GET: api/dispatch/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DispatchRequest>> GetById(Guid id, CancellationToken ct)
        {
            var entity = await _db.DispatchRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null)
                return NotFound();

            return Ok(entity);
        }

        // GET: api/dispatch
        [HttpGet]
        public async Task<ActionResult<List<DispatchRequest>>> List(CancellationToken ct)
        {
            var items = await _db.DispatchRequests
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(100)
                .ToListAsync(ct);

            return Ok(items);
        }
    }
}
