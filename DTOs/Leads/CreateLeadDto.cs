
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.DTOs.Leads;

public class CreateLeadDto
{
    [Required]
    [StringLength(150)]
    public string LeadName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(150)]
    public string? CompanyName { get; set; }

    [StringLength(100)]
    public string? Source { get; set; }

    [StringLength(30)]
    public string? Status { get; set; }

    [StringLength(30)]
    public string? Priority { get; set; }

    public string? Notes { get; set; }
}
