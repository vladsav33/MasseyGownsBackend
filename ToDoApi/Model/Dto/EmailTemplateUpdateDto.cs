namespace GownApi.Model.Dto
{
    public class EmailTemplateUpdateDto
    {
        public string SubjectTemplate { get; set; } = null!;
        public string BodyHtml { get; set; } = null!;
        public string TaxReceiptHtml { get; set; } = null!;
        public string CollectionDetailsHtml { get; set; } = "";
    }
}
