namespace GownApi.Model.Dto
{
    public class AddressLabelDto
    {
        public string LabelType { get; set; } = "";  
        public int SourceId { get; set; }             

        public string ToName { get; set; } = "";
        public string? Attn { get; set; }
        public string? Phone { get; set; }

        public string Address1 { get; set; } = "";
        public string? Address2 { get; set; }

        public string City { get; set; } = "";
        public string Postcode { get; set; } = "";
    }

}
