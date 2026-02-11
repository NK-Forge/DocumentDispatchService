namespace DocumentDispatchService.Contracts
{
    public sealed class CreateDispatchRequest
    {
        public string RecipientEmail { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
    }
}
