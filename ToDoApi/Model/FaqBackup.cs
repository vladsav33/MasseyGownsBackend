using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class FaqBackup
{
    public int? Id { get; set; }

    public string? Question { get; set; }

    public string? Answer { get; set; }

    public string? Category { get; set; }
}
