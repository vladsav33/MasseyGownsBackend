using GownApi.Services;
namespace GownApi.Model
{
    public class Items : IItemBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Type { get; set; }
        public byte[]? Picture { get; set; }
        public decimal? HirePrice { get; set; }
        public decimal? BuyPrice { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public bool IsHiring { get; set; }
      
    }
}
