using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class FaqNew
{
    public int FaqId { get; set; }

    public string Question { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public string? Category { get; set; }
}
