using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class CmsContentBlock
{
    public int Id { get; set; }

    public string Page { get; set; } = null!;

    public string Section { get; set; } = null!;

    public string Key { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string Label { get; set; } = null!;

    public string? Value { get; set; }

    public DateTime UpdatedAt { get; set; }
}
