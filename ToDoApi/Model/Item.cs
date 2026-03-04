using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class Item
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public byte[]? Picture { get; set; }

    public decimal? HirePrice { get; set; }

    public decimal? BuyPrice { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public bool? IsHiring { get; set; }

    public string? Type { get; set; }

    public virtual ICollection<CeremonyDegreeItem> CeremonyDegreeItems { get; set; } = new List<CeremonyDegreeItem>();

    public virtual ICollection<HoodType> HoodTypes { get; set; } = new List<HoodType>();

    public virtual ICollection<Size> Sizes { get; set; } = new List<Size>();
}
