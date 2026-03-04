using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class HomePageSetting
{
    public int Id { get; set; }

    public string? HeroImageUrl { get; set; }

    public DateTime UpdateAt { get; set; }

    public string? CeremonyText { get; set; }

    public string? CeremonyImageUrl { get; set; }
}
