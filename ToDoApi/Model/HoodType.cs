using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class HoodType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int? ItemId { get; set; }

    public string? ShortName { get; set; }

    public string? HoodNote { get; set; }

    public decimal? HoodBin { get; set; }

    public virtual Item? Item { get; set; }
    public bool Doctoral { get; set; }
}
