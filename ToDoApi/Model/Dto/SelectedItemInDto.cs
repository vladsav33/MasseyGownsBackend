namespace GownApi.Model.Dto
{
    public class SelectedItemInDto
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public int? SizeId { get; set; }
        public int? HatId { get; set; }
        public int? FitId { get; set; }
        public int? HoodId { get; set; }
        public decimal Cost { get; set; }
    }
}
