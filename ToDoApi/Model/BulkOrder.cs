using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class BulkOrder
{
    public int Id { get; set; }

    public string? LastName { get; set; }

    public string? FirstName { get; set; }

    public decimal? HeadSize { get; set; }

    public decimal? Height { get; set; }

    public string? HatType { get; set; }

    public string? GownType { get; set; }

    public string? HoodType { get; set; }

    public string? UcolSash { get; set; }

    public int? CeremonyId { get; set; }

    public DateOnly? OrderDate { get; set; }

    public virtual Ceremony? Ceremony { get; set; }
}
