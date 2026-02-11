using DocumentDispatchService.Contracts;
using DocumentDispatchService.Models;
using Microsoft.AspNetCore.Mvc;

namespace DocumentDispatchService.Controllers
{

    [ApiController]
    [Route("dispatch")]
    public class DispatchController : ControllerBase
    {
        // In-memory store for now;  I'll replace with EF Core next
        private static readonly List<DispatchRequest> Store = new();

        [HttpGet]
        public ActionResult<List<DispatchRequest>> GetAll()
        {
            return Ok(Store.OrderByDescending(x => x.CreatedAtUtc).ToList());
        }

        [HttpGet("{id:guid}")]
        public ActionResult<DispatchRequest> GetOne(Guid id)
        {
            var item = Store.FirstOrDefault(x => x.Id == id);
            return item is null ? NotFound() : Ok(item);
        }

        [HttpPost]
        public ActionResult<DispatchRequest> Create([FromBody] CreateDispatchRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.RecipientEmail))
                return BadRequest("RecipientEmail is requered.");

            if (string.IsNullOrWhiteSpace(body.DocumentName))
                return BadRequest("DocumentName is required.");

            var req = new DispatchRequest
            {
                RecipientEmail = body.RecipientEmail.Trim(),
                DocumentName = body.DocumentName.Trim(),
                Status = DispatchStatus.Pending,
                RetryCount = 0,
                CreatedAtUtc = DateTime.UtcNow,
                UpdateAtUtc = DateTime.UtcNow
            };

            Store.Add(req);

            return CreatedAtAction(nameof(GetOne), new { id = req.Id }, req);
        }
    }
}
