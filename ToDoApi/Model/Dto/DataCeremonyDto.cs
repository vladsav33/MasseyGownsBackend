namespace GownApi.Model.Dto
{
    public class DataCeremonyDto
    {
        public long Id { get; set; }
        public int Year { get; set; }
        public string Location { get; set; }
        public string Ceremony_name { get; set; }
        public string Student_ID { get; set; }
        public string Forename { get; set; }
        public string Surname { get; set; }
        public string Full_Name { get; set; }
        public string Programme_code { get; set; }
        public string Programme_Description { get; set; }
        public string? Mobile { get; set; }
    }
}
