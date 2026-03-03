using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class Set
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public decimal? BuyPrice { get; set; }
}
