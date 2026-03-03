using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class CeremonyDegreeItem
{
    public int Id { get; set; }

    public int? CeremonyDegreeId { get; set; }

    public int? ItemId { get; set; }

    public bool? Active { get; set; }

    public virtual CeremonyDegree? CeremonyDegree { get; set; }

    public virtual Item? Item { get; set; }
}
