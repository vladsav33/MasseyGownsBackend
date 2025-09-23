using System;

namespace GownApi
{
    public class Contacts
    {
        public string Id { get; set; }             
        public string Email { get; set; }       
        public string FirstName { get; set; }    
        public string LastName { get; set; }     
        public string Subject { get; set; }    
        public string Query { get; set; }        
        public DateTime CreatedAt { get; set; }  
    }
}
