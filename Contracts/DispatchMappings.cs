using DocumentDispatchService.Models;

namespace DocumentDispatchService.Contracts
{
    public static class DispatchMappings
    {
        public static DispatchResponse ToResponse(this DispatchRequest entity)
        {
            return new DispatchResponse
            {
                Id = entity.Id,
                RecipientEmail = entity.RecipientEmail,
                DocumentName = entity.DocumentName,
                Status = entity.Status.ToString(),
                RetryCount = entity.RetryCount,
                CreatedAtUtc = entity.CreatedAtUtc,
                UpdatedAtUtc = entity.UpdatedAtUtc,
                LastError = entity.LastError
            };
        }
    }
}
