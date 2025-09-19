namespace GownsApi
{
    public class MenuItem
    {
        public int Id {  get; set; }
        public string Name { get; set; }
        public List<MenuItem> Children { get; set; } = new List<MenuItem>();
    }
}
