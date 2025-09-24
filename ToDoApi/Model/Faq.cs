namespace GownApi.Model
{
    public class Faq
    {
        public int Id { get; set; }
        public string Question { get; set; }
        public string Answer { get; set; }
        public string? Category { get; set; }
    }
}
