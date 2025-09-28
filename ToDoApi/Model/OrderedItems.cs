using System.Runtime.Intrinsics.X86;

namespace GownApi.Model
{
    public class OrderedItems
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int SkuId { get; set; }
        public float Cost { get; set; }
        public short Quantity { get; set; }
        public bool Hire { get; set; }
    }
}
