using System.ComponentModel.DataAnnotations;

namespace DocumentDispatchService.Contracts
{
    public sealed class CreateDispatchRequest
    {
        [Required]
        [EmailAddress]
        [MaxLength(320)]
        public string RecipientEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string DocumentName { get; set; } = string.Empty;
    }
}
