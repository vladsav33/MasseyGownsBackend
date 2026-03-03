using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class OrderedItem
{
    public int Id { get; set; }

    public int? OrderId { get; set; }

    public int? SkuId { get; set; }

    public decimal? Cost { get; set; }

    public decimal? Quantity { get; set; }

    public bool? Hire { get; set; }
}
