namespace SendEmailService.Models
{
    public class EmailMessage
    {
        public required string To { get; set; }
        public required string Subject { get; set; }
        public required string Body { get; set; }
        public string? Type { get; set; }
    }
}
