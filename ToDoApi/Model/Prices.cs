namespace GownApi.Model
{
    public class Prices
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string PriceCode { get; set; }
        public float Hood { get; set; }
        public float Gown { get; set; }
        public float Hat { get; set; }
        public float XtraHood { get; set; }
        public float UcolSash { get; set; }
        public float? Freight { get; set; }
        public string PriceNote { get; set; }
    }
}
