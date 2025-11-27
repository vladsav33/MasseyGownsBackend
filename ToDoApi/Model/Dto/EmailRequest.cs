namespace GownApi.Model.Dto
{
    public class EmailRequest
    {
        public string To { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }

        // Optional
        public string From { get; set; }
    }
}
