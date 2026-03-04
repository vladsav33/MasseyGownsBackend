using System;
using System.Collections.Generic;

namespace GownApi.Model;

public partial class EmailTemplatesBackup
{
    public int? Id { get; set; }

    public string? Name { get; set; }

    public string? SubjectTemplate { get; set; }

    public string? BodyHtml { get; set; }

    public string? TaxReceiptHtml { get; set; }

    public string? CollectionDetailsHtml { get; set; }
}
