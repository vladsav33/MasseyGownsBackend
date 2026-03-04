using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class TempHood
{
    public long Id { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Note { get; set; }

    public decimal? Bin { get; set; }
}
