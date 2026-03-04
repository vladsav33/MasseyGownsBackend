using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class Fit
{
    public int Id { get; set; }

    public string FitType { get; set; } = null!;

    public int? ItemId { get; set; }

    public virtual ICollection<Size> Sizes { get; set; } = new List<Size>();
}
