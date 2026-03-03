using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class Price
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string PriceCode { get; set; } = null!;

    public decimal Hood { get; set; }

    public decimal Gown { get; set; }

    public decimal Hat { get; set; }

    public decimal? XtraHood { get; set; }

    public decimal UcolSash { get; set; }

    public decimal? Freight { get; set; }

    public string? PriceNote { get; set; }
}
