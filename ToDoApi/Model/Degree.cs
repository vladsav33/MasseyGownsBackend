using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class Degree
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Labeldegree { get; set; }

    public decimal? DegreeOrder { get; set; }

    public virtual ICollection<CeremonyDegree> CeremonyDegrees { get; set; } = new List<CeremonyDegree>();
}
