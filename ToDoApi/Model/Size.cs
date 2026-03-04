using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class Size
{
    public int Id { get; set; }

    public string Size1 { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public int? ItemId { get; set; }

    public int? FitId { get; set; }

    public decimal? GownSize { get; set; }

    public string? StoleSize { get; set; }

    public string? Labelsize { get; set; }

    public decimal? Price { get; set; }

    public virtual Fit? Fit { get; set; }

    public virtual Item? Item { get; set; }
}
