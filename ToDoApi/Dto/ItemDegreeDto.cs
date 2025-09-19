using Microsoft.AspNetCore.Connections.Features;
using GownApi.Services;

namespace GownApi.Dto
{
    public class ItemDegreeDto
    {
        public int Id { get; set; }
        public int? DegreeId { get; set; }
        public string Name { get; set; }
        public string? PictureBase64 { get; set; }
        public float? HirePrice { get; set; }
        public float? BuyPrice { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public bool IsHiring { get; set; }
    }
}
