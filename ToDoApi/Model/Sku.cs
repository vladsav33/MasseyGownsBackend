namespace GownApi.Model
{
    public class Sku
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public int? SizeId { get; set; }
        public int? FitId { get; set; }
        public int? HoodId { get; set; }
        public int Count { get; set; }
    }
}
