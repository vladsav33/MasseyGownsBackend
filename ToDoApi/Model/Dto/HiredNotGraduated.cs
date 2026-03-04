namespace GownApi.Model.Dto
{
    public class HiredNotGraduated
    {
        public long Id { get; set; }
        public long StudentId { get; set; }
        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? Degree { get; set; }
        public string? CeremonyName { get; set; }
        public string? HoodName { get; set; }
        public string? ReferenceNo { get; set; }
        public string? Mobile { get; set; }
    }
}
