using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class BulkOrderLabel
{
    public int Id { get; set; }

    public string? IdCode { get; set; }

    public string? HoodType { get; set; }
}
