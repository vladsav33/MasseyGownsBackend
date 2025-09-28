namespace GownApi.Model.Dto
{
    public class SelectedItem
    {
        public int ItemId { get; set; }
        public int? SizeId { get; set; }
        public int? FitId { get; set; }
        public int? HoodId { get; set; }
        public bool Hire {  get; set; }
        public short Quantity { get; set; }
    }
}
