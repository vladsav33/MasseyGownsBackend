using Azure.Storage.Queues;
using System.Text.Json;

namespace GownApi.Services;

public record EmailJob(
    string Type,         // "PaymentCompleted" | "RefundCompleted"
    int? OrderId,        //Null for ContactQuery email jobs
    string? ReferenceNo, //Null for ContactQuery email jobs
    string? TxnId,       //Null for ContactQuery email and & purchase order jobs
    DateTimeOffset? OccurredAt
);

public interface IQueueJobPublisher
{
    Task EnqueueEmailJobAsync(EmailJob job, CancellationToken ct = default);
}

public class QueueJobPublisher : IQueueJobPublisher
{
    private readonly QueueClient _queue;

    public QueueJobPublisher(IConfiguration config)
    {
        var conn = config["BlobStorage:ConnectionString"];
        var queueName = config["BlobStorage:EmailJobsQueueName"] ?? "email-jobs";

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
        var json = JsonSerializer.Serialize(job);
        await _queue.SendMessageAsync(json, cancellationToken: ct);
    }
}
