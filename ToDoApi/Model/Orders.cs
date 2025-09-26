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
        public int SizeId { get; set; }
        public bool Paid { get; set; }
        public int? PaymentMethod { get; set; }
        public string? PurchaseOrder { get; set; }
        public DateOnly OrderDate { get; set; }
    }
}
