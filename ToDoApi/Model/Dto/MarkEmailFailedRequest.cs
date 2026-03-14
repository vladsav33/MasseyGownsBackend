namespace GownApi.Model.Dto
{
    public record MarkEmailFailedRequest(
        int EmailQueueItemId,
        string? LastError
    );
}
