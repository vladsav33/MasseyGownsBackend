namespace GownApi.Model
{
    public class CeremonyDetails
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateOnly? CeremonyDate { get; set; }
        public DateOnly? CeremonyDate2 { get; set; }
        public int? CerenonyNo { get; set; }
        public DateOnly? DueDate { get; set; }
        public bool Visible { get; set; }
        public string? IdCode { get; set; }
        public string? InstitutionName { get; set; }
        public string? CourierAddress { get; set; }
        public string? PostalAddress { get; set; }
        public string? PostalAddress2 { get; set; }
        public string? PostalAddress3 { get; set; }
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
        public string? CollectionTime { get; set; }
        public string? Content { get; set; }
        public int hat_count { get; set; }
        public int hood_count { get; set; }
        public int gown_count { get; set; }
        public int ucol_count { get; set; }
    }
}
