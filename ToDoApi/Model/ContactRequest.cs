namespace GownApi.Model
{
    public class ContactRequest
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Subject { get; set; }
        public string Enquiry { get; set; }

        public string ToEmail { get; set; }   
    }
}
