namespace GownApi.Model.Dto
{
    public class InternalFormListDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string OrderType { get; set; }
        public DateOnly OrderDate { get; set; }
        public bool Paid { get; set; }
        public float? AmountPaid { get; set; }
        public string Address { get; set; }
        public string ContactNo { get; set; }
        public string OrderNo { get; set; }
    }
}
