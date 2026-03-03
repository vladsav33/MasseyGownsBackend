using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class OrdersNotDatum
{
    public long Id { get; set; }

    public string? ClientId { get; set; }

    public string? Surname { get; set; }

    public string? Forename { get; set; }

    public string? Qual { get; set; }

    public string? Ceremony { get; set; }

    public string? Note { get; set; }

    public string? ReferenceNo { get; set; }

    public string? Mobile { get; set; }
}
