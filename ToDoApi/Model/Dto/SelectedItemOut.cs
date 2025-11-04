namespace GownApi.Model.Dto
{
    public class SelectedItemOut
    {
        public int Id { get; set; }
        public string ItemName { get; set; }
        public string? SizeName { get; set; }
        public string? Labelsize {  get; set; }
        public string? Labeldegree { get; set; }
        public string? FitName { get; set; }
        public string? HoodName { get; set; }
        public bool Hire {  get; set; }
        public short Quantity { get; set; }
    }
}
