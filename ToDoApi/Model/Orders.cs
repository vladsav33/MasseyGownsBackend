namespace GownApi.Model
{
    public class Orders
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Postcode { get; set; }
        public string Country { get; set; }
        public string Phone { get; set; }
        public string Mobile { get; set; }
        public int StudentId { get; set; }
        public string? Message { get; set; }
        public bool Paid { get; set; }
        public int? PaymentMethod { get; set; }
        public string? PurchaseOrder { get; set; }
        public DateOnly OrderDate { get; set; }
        public int? CeremonyId { get; set; }
        public int? DegreeId { get; set; }
        public string? OrderType { get; set; }
        public string? Note { get; set; }
        public string? Changes { get; set; }
        public string? PackNote { get; set; }
        public float? AmountPaid { get; set; }
        public float? AmountOwning {  get; set; }
        public float? Donation {  get; set; }
        public float? Freight { get; set; }
        public float? Refund { get; set; }
        public float? AdminCharges { get; set; }
        public DateOnly? PayBy { get; set; }
        public string? Reference_no { get; set; }
    }
}
