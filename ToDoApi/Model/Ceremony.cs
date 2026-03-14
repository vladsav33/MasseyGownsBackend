using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class Ceremony
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public DateOnly? CeremonyDate { get; set; }

    public bool Visible { get; set; }

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

    public int? PriceCode { get; set; }

    public decimal? Freight { get; set; }

    public string? CollectionTime { get; set; }

    public string? Content { get; set; }

    public DateOnly? CeremonyDate2 { get; set; }

    public int? CerenonyNo { get; set; }

    public string? PostalAddress2 { get; set; }

    public string? PostalAddress3 { get; set; }

    public long? PriceId { get; set; }

    public virtual ICollection<BulkOrder> BulkOrders { get; set; } = new List<BulkOrder>();

    public virtual ICollection<CeremonyDegree> CeremonyDegrees { get; set; } = new List<CeremonyDegree>();
}
