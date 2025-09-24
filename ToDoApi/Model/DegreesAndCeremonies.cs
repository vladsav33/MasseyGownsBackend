namespace GownApi.Model
{
    public class DegreesAndCeremonies
    {
        public int Id { get; set; }
        public DateOnly CeremonyDate { get; set; }
        public string CeremonyName { get; set; }
        public string DegreeName { get; set; }
    }
}
