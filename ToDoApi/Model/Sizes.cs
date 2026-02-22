namespace GownApi.Model
{
    public class Sizes
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public int? FitId { get; set; }
        public string? FitName { get; set; }
        public string Size { get; set; }
        public string? Labelsize { get; set; }
        public float? Price { get; set; }

    }
}
