using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class User
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public bool? Active { get; set; }

    public string Role { get; set; } = null!;

    public bool? Approver { get; set; }
}
