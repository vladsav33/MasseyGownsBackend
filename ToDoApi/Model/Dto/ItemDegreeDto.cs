namespace GownApi.Model.Dto
{
    public class ItemDegreeDto
    {
        public int Id { get; set; }
        public int? DegreeId { get; set; }
        public string DegreeName { get; set; }
        public string Name { get; set; }
        public string? PictureBase64 { get; set; }
        public decimal? HirePrice { get; set; }
        public decimal? BuyPrice { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public bool IsHiring { get; set; }
        public bool Active { get; set; }
    }
}
