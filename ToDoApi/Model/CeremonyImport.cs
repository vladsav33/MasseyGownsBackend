using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class CeremonyImport
{
    public long Id { get; set; }

    public decimal? Year { get; set; }

    public string? Location { get; set; }

    public string? CeremonyName { get; set; }

    public string? StudentId { get; set; }

    public string? Forename { get; set; }

    public string? Surname { get; set; }

    public string? FullName { get; set; }

    public string? ProgramCode { get; set; }

    public string? ProgramDesc { get; set; }

    public string? Mobile { get; set; }
}
