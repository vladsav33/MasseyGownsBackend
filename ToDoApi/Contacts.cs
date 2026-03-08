using System;

namespace GownApi
{
    public class Contacts
    {
        public required string Id { get; set; }             
        public required string Email { get; set; }       
        public required string FirstName { get; set; }    
        public required string LastName { get; set; }     
        public required string Subject { get; set; }    
        public required string Query { get; set; }        
        public DateTimeOffset CreatedAt { get; set; }  
    }
}
