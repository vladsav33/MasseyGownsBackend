using Azure.Storage.Queues;
using GownApi.Model;
using System.Text.Json;

namespace GownApi.Services;

public record EmailJob(
    string Type,        
    int? OrderId,      // contact query email has no order id 
    string? ReferenceNo, 
    string? TxnId,       
    DateTimeOffset? OccurredAt,
    int? EmailQueueItemId
);

public interface IQueueJobPublisher
{
    Task EnqueueEmailJobAsync(EmailJob job, CancellationToken ct = default);
}

public class QueueJobPublisher : IQueueJobPublisher
{
    private readonly QueueClient _queue;
    private readonly GownDb _db;

    public QueueJobPublisher(IConfiguration config, GownDb db)
    {
        
        var conn = config["BlobStorage:ConnectionString"];
        var queueName = config["BlobStorage:EmailJobsQueueName"] ?? "email-jobs";
        _db = db;

        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("BlobStorage:ConnectionString not configured.");

        _queue = new QueueClient(conn, queueName, new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.Base64
        });

        _queue.CreateIfNotExists();
    }

    public async Task EnqueueEmailJobAsync(EmailJob job, CancellationToken ct = default)
    {
        string? paymeUrl = null;

        if (job.Type == "PaymentReminder1" || job.Type == "PaymentReminder2")
        {
            paymeUrl = job.TxnId;
        }

        var emailQueueItem = new EmailQueueItems
        {
            OrderId = job.OrderId,
            EmailType = job.Type,
            PayMeUrl = paymeUrl,
            EmailBody = null,
            Status = EmailStatus.Pending,
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _db.EmailQueueItems.Add(emailQueueItem);
        await _db.SaveChangesAsync(ct);

        var jobToSend = job with
        {
            EmailQueueItemId = emailQueueItem.Id
        };

        var json = JsonSerializer.Serialize(jobToSend);
        await _queue.SendMessageAsync(json, cancellationToken: ct);
    }

}
