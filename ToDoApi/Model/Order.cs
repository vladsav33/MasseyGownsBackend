using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class Order
{
    public int Id { get; set; }

    public string Address { get; set; } = null!;

    public bool Paid { get; set; }

    public DateOnly OrderDate { get; set; }

    public string? Email { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? City { get; set; }

    public string? Postcode { get; set; }

    public string? Country { get; set; }

    public decimal? StudentId { get; set; }

    public string? Phone { get; set; }

    public string? Mobile { get; set; }

    public string? Message { get; set; }

    public decimal? PaymentMethod { get; set; }

    public string? PurchaseOrder { get; set; }

    public int? DegreeId { get; set; }

    public int? CeremonyId { get; set; }

    public string? OrderType { get; set; }

    public string? Note { get; set; }

    public string? Changes { get; set; }

    public decimal? AmountPaid { get; set; }

    public decimal? AmountOwning { get; set; }

    public decimal? Donation { get; set; }

    public decimal? Freight { get; set; }

    public decimal? Refund { get; set; }

    public decimal? AdminCharges { get; set; }

    public DateOnly? PayBy { get; set; }

    public string? PackNote { get; set; }

    public string? ReferenceNo { get; set; }

    public decimal? Status { get; set; }

    public string? Region { get; set; }

    public string? GstNo { get; set; }

    public bool Refunded { get; set; }

    public decimal? RefundedAmount { get; set; }

    public string? RefundTxnId { get; set; }

    public DateTime? RefundedAt { get; set; }

    public DateTime? RefundEmailSentAt { get; set; }

    public string? PaymentTxnId { get; set; }

    public int? RefundLastEc { get; set; }

    public string? RefundLastEm { get; set; }

    /// <summary>
    ///    None = 0,
    ///    InProgress = 1,
    ///    Completed = 2,
    ///    Failed = 3,
    ///    Requested = 4
    /// </summary>
    public short RefundStatusCode { get; set; }
}
