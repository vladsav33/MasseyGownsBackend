using Microsoft.EntityFrameworkCore;

namespace GownApi.Services
{
    public class PaymentReminderJob
    {
        private readonly GownDb _db;
        private readonly IQueueJobPublisher _publisher;
        private readonly PaystationPayMeService _paystationPayMeService;

        public PaymentReminderJob(GownDb db, IQueueJobPublisher publisher, PaystationPayMeService paystationPayMeService)
        {
            _db = db;
            _publisher = publisher;
            _paystationPayMeService = paystationPayMeService;
        }

        public async Task RunAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var firstReminderThreshold = now.AddHours(-1);
            var secondReminderThreshold = now.AddDays(-5);

            var firstReminderOrders = await _db.orders
                .Where(o =>
                    o.Paid != true &&
                    o.PaymentMethod == 1 &&
                    o.Email != null &&
                    o.Email != "" &&
                    o.CreatedAt != null &&
                    o.PaymentReminder1SentAt == null &&
                    o.CreatedAt <= firstReminderThreshold &&
                    o.CreatedAt > secondReminderThreshold)
                .ToListAsync(ct);

            var secondReminderOrders = await _db.orders
                .Where(o =>
                o.Paid != true &&
                o.PaymentMethod == 1 &&
                o.Email != null &&
                o.Email != "" &&
                o.CreatedAt != null &&
                o.PaymentReminder2SentAt == null &&
                o.CreatedAt <= secondReminderThreshold)
                .ToListAsync(ct);

            foreach (var order in firstReminderOrders)
            {
                var paymeUrl = await _paystationPayMeService.CreatePayMeUrlAsync(order, ct);

                await _publisher.EnqueueEmailJobAsync(new EmailJob(
                    Type: "PaymentReminder1",
                    OrderId: order.Id,
                    ReferenceNo: $"Ref-{order.Id}",
                    TxnId: paymeUrl,
                    OccurredAt: DateTimeOffset.UtcNow,
                    EmailQueueItemId: null
                ), ct);

                order.PaymentReminder1SentAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);

            foreach (var order in secondReminderOrders)
            {
                var firstReminderEmail = await _db.EmailQueueItems
                    .Where(x => x.OrderId == order.Id && x.EmailType == "PaymentReminder1" && x.PayMeUrl != null)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefaultAsync(ct);

                if (string.IsNullOrWhiteSpace(firstReminderEmail?.PayMeUrl))
                {
                    continue;
                }

                await _publisher.EnqueueEmailJobAsync(new EmailJob(
                    Type: "PaymentReminder2",
                    OrderId: order.Id,
                    ReferenceNo: $"Ref-{order.Id}",
                    TxnId: firstReminderEmail.PayMeUrl,
                    OccurredAt: DateTimeOffset.UtcNow,
                    EmailQueueItemId: null
                ), ct);

                order.PaymentReminder2SentAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
        }
    }
}