
namespace AcxiomCRM.Web.DTOs.Leads;

public class LeadResponseDto
{
    public int LeadId { get; set; }
    public string? LeadCode { get; set; }
    public string LeadName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public string? Source { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
}
