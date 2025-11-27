namespace GownApi.Model
{
    public class EmailSettings
    {
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public bool EnableSsl { get; set; }

        public string UserName { get; set; }
        public string Password { get; set; }

        public string From { get; set; }
        public string To { get; set; }
    }
}
