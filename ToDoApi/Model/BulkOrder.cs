namespace GownApi.Model
{
    public class BulkOrder
    {
        public int Id { get; set; }
        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public float? HeadSize { get; set; }
        public float? Height { get; set; }
        public string? HatType { get; set; }
        public string? GownType { get; set; }
        public string? HoodType { get; set; }
        public string? UcolSash {  get; set; }
        public int CeremonyId { get; set; }
    }
}
