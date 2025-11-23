using System;

namespace GownApi.Model
{
    public class HomePageSettings
    {
        public int Id { get; set; }
        public string? HeroImageUrl { get; set; }
        public string? CeremonyImageUrl { get; set; }
        public string? CeremonyText { get; set; }
        public DateTime UpdateAt { get; set; }
    }
}
