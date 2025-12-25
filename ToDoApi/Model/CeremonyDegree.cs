namespace GownApi.Model
{
    public class CeremonyDegree
    {
        public int Id { get; set; }
        public int GraduationId { get; set; }
        public int DegreeId { get; set; }
        public bool? Active { get; set; }
    }
}
