using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class Contact
{
    public string Id { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string Query { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }
}
