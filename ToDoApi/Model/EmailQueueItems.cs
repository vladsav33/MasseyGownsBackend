namespace GownApi.Model
{
    public class EmailQueueItems
    {
        public int Id { get; set; }

        public int? OrderId { get; set; }

        public string EmailType { get; set; }

        public string? ToEmail { get; set; }

        public string? Subject { get; set; }

        public string? PayMeUrl { get; set; }
        public string? EmailBody { get; set; }

        public EmailStatus Status { get; set; }

        public int AttemptCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ProcessingStartedAt { get; set; }

        public DateTime? SentAt { get; set; }

        public string? LastError { get; set; }
    }
}
