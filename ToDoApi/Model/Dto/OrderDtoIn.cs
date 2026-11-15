namespace GownApi.Model.Dto
{
    public class OrderDtoIn
    {
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }
        public decimal? AmountOwing { get; set; }
        public decimal? AmountPaid { get; set; }
        public decimal? Bond { get; set; }
        public string? Bonnet { get; set; }
        public string? City { get; set; }
        public string? Comments { get; set; }
        public string? Company { get; set; }
        public string? Country { get; set; }
        public string? Donation { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? GarmentBag { get; set; }
        public Dictionary<string, string?>[]? Gown { get; set; }
        public Dictionary<string, string?>? Hood { get; set; }
        public string? HoodPin { get; set; }
        public string? Keyring { get; set; }
        public string? Korowai { get; set; }
        public string? LastName { get; set; }
        public string? Mortarboard { get; set; }
        public string? OrderNumber { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentReference { get; set; }
        public string? Phone { get; set; }
        public string? Photography { get; set; }
        public string? PrepaidReturn { get; set; }
        public string? PurchaseOrder { get; set; }
        public string? Qualification { get; set; }
        public Dictionary<string, string?>? Stole { get; set; }
        public bool? Staff { get; set; }
        public string? State { get; set; }
        public string? StudentId { get; set; }
        public string? Supplier { get; set; }
        public string? Tiepin { get; set; }
        public decimal? TotalCost { get; set; }
        public Dictionary<string, string?>? Trencher { get; set; }
        public string? University { get; set; }
        public string? Zip { get; set; }
    }
}
