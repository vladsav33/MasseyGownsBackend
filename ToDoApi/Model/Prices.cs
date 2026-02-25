namespace GownApi.Model
{
    public class Prices
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string PriceCode { get; set; }
        public decimal? Hood { get; set; }
        public decimal? Gown { get; set; }
        public decimal? Hat { get; set; }
        public decimal? XtraHood { get; set; }    
        public decimal? UcolSash { get; set; }
        public decimal? Freight { get; set; }
        public string? PriceNote { get; set; }
    }
}
