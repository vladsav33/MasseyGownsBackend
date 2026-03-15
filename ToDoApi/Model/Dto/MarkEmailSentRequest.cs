namespace GownApi.Model.Dto
{
    public record MarkEmailSentRequest(
        int EmailQueueItemId,
        string? ToEmail,
        string? Subject,
        string? EmailBody
    );
}
