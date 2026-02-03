namespace GownApi.Model
{
    public class SkuDetail
    {
        public int Id {  get; set; }
        public string Name { get; set; }
        public string? Size { get; set; }
        public string? Labelsize { get; set; }
        public string? FitType { get; set; }
        public string? Hood { get; set; }
        public int? Count { get; set; }
    }
}
