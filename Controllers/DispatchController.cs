using DocumentDispatchService.Contracts;
using DocumentDispatchService.Data;
using DocumentDispatchService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocumentDispatchService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public sealed class DispatchController : ControllerBase
    {
        private readonly DispatchDbContext _db;
        private readonly ILogger<DispatchController> _logger;

        public DispatchController(DispatchDbContext db, ILogger<DispatchController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // POST: api/dispatch
        [HttpPost]
        [ProducesResponseType(typeof(DispatchResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DispatchResponse>> Create([FromBody] CreateDispatchRequest request, CancellationToken ct)
        {
            _logger.LogInformation(
                "Create dispatch request received.  RecipientEmail={RecipientEmail}, DocumentName={DocumentName}",
                request.RecipientEmail,
                request.DocumentName
            );

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

            _logger.LogInformation(
                "Dispatch request created. DispatchId={DispatchId}, Status={Status}",
                entity.Id,
                entity.Status
            );

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity.ToResponse());
        }

        // GET: api/dispatch/{id}
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(DispatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DispatchResponse>> GetById(Guid id, CancellationToken ct)
        {
            _logger.LogInformation("Get dispatch request by id. DispatchId={DispatchId}", id);


            var entity = await _db.DispatchRequests.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (entity is null)
            {
                _logger.LogWarning("Dispatch request not found. DispatchId={DispatchId}", id);
                return NotFound();
            }

            _logger.LogInformation(
                "Dispatch request found. DispatchId={DispatchId}, Status={Status}",
                entity.Id,
                entity.Status
            );

            return Ok(entity.ToResponse());
        }

        // GET: api/dispatch
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<DispatchResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResponse<DispatchResponse>>> List(
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50,
            CancellationToken ct = default)
        {
            if (skip < 0) skip = 0;
            if (take <= 0) take = 50;
            if (take > 200) take = 200;

            _logger.LogInformation("List dispatch requests. Skip={Skip}, Take={Take}", skip, take);

            var items = await _db.DispatchRequests
                .OrderByDescending(x => x.CreatedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(ct);

            var mapped = items.Select(x => x.ToResponse()).ToList();

            _logger.LogInformation("List dispatch requests returned. Count={Count}", mapped.Count);

            return Ok(new PagedResponse<DispatchResponse>
            {
                Skip = skip,
                Take = take,
                Count = mapped.Count,
                Items = mapped
            });
        }
    }
}
