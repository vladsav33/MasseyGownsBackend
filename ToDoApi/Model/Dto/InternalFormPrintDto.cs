namespace GownApi.Model.Dto
{
    public class InternalFormPrintDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";       // orderType
        public string Date { get; set; } = "";       // yyyy-MM-dd
        public string Name { get; set; } = "";
        public string AddressLine1 { get; set; } = "";
        public string ContactNo { get; set; } = "";
        public string FormDateText { get; set; } = "";  // Wednesday, 7 May 2025

        public int StudentId { get; set; }            // Access No (暂用)
        public string Email { get; set; } = "";

        public string WebOrderNo { get; set; } = "";

        public string ReceiptNo { get; set; } = "";

        public bool Paid { get; set; }
        public float? AmountPaid { get; set; }

        public List<InternalFormPrintItemDto> Items { get; set; } = new();
    }

    public class InternalFormPrintItemDto
    {
        public int SkuId { get; set; }
        public short Quantity { get; set; }
        public bool Hire { get; set; }
        public float? Cost { get; set; }
        public string ItemName { get; set; } = "";
        public string SizeName { get; set; } = "";
        public string ItemType { get; set; } = "";

    }
}
