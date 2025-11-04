namespace GownApi.Model
{
    public class Ceremonies
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateOnly? DueDate { get; set; }
        public bool Visible { get; set; }
    }
}
