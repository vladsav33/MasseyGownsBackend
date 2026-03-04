namespace GownApi.Model.Dto
{
    public class OrderDtoOut
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string? Region { get; set; }
        public string Postcode { get; set; }
        public string Country { get; set; }
        public string Phone { get; set; }
        public string Mobile { get; set; }
        public int StudentId { get; set; }
        public string? Message { get; set; }
        public SelectedItemOut[] Items { get; set; }
        public bool Paid { get; set; }
        public int? PaymentMethod { get; set; }
        public string? PurchaseOrder { get; set; }
        public DateOnly OrderDate { get; set; }
        public int? CeremonyId { get; set; }
        public string? Ceremony {  get; set; }
        public int? DegreeId { get; set; }
        public string? OrderType { get; set; }
        public string? Note { get; set; }
        public string? Changes { get; set; }
        public string? PackNote { get; set; }
        public decimal? AmountPaid { get; set; }
        public decimal? AmountOwning { get; set; }
        public decimal? Donation { get; set; }
        public decimal? Freight { get; set; }
        public decimal? Refund { get; set; }
        public decimal? AdminCharges { get; set; }
        public DateOnly? PayBy { get; set; }
        public int? Status { get; set; }
        public string? ReferenceNo { get; set; }

        public decimal? RefundedAmount { get; set; }// Added for refund details
        public string? RefundTxnId { get; set; }
        public DateTime? RefundedAt { get; set; }

        public DateTime? RefundEmailSentAt { get; set; }
        public string? PaymentTxnId { get; set; }

        public int? RefundLastEc { get; set; }
        public string? RefundLastEm { get; set; }
        public RefundStatusCode RefundStatusCode { get; set; }

    }
}
