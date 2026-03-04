using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class CeremonyDegree
{
    public int Id { get; set; }

    public int? GraduationId { get; set; }

    public int? DegreeId { get; set; }

    public bool? Active { get; set; }

    public virtual ICollection<CeremonyDegreeItem> CeremonyDegreeItems { get; set; } = new List<CeremonyDegreeItem>();

    public virtual Degree? Degree { get; set; }

    public virtual Ceremony? Graduation { get; set; }
}
