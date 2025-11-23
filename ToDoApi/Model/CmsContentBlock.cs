namespace GownApi.Model
{
    public class CmsContentBlock
    {
        public int Id { get; set; }
        public string Page { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Value { get; set; } 
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
