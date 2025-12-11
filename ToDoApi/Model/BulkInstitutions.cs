namespace GownApi.Model
{
    public class BulkInstitutions
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateOnly? CeremonyDate { get; set; }
        public DateOnly? DueDate { get; set; }
        public string? IdCode { get; set; }
        public string? InstitutionName { get; set; }
        public string? CourierAddress { get; set; }
        public string? PostalAddress { get; set; }
        public string? City { get; set; }
        public DateOnly? DespatchDate { get; set; }
        public DateOnly? DateSent { get; set; }
        public DateOnly? ReturnDate { get; set; }
        public DateOnly? DateReturned { get; set; }
        public string? Organiser { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? InvoiceEmail { get; set; }
        public string? PriceCode { get; set; }
        public float? Freight { get; set; }
    }
}
