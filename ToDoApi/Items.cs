﻿using GownApi.Services;
namespace GownsApi
{
    public class Items : IItemBase
    {
        public int Id { get; set; }
        public int? DegreeId { get; set; }
        public string Name { get; set; }
        public byte[]? Picture { get; set; }
        public float? HirePrice { get; set; }
        public float? BuyPrice { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public bool IsHiring { get; set; }
    }
}
