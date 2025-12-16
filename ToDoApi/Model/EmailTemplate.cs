namespace GownApi.Model
{
    public class EmailTemplate
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string SubjectTemplate { get; set; } = null!;
        public string BodyHtml { get; set; } = null!;
        public string TaxReceiptHtml { get; set; } = null!;
        public string CollectionDetailsHtml { get; set; } = "";
    }
}
