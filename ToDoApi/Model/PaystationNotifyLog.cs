using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class PaystationNotifyLog
{
    public long Id { get; set; }

    public string TransactionId { get; set; } = null!;

    public DateTime ReceivedAt { get; set; }

    public string RawXml { get; set; } = null!;
}
