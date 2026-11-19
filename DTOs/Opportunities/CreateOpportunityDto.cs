
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.DTOs.Opportunities;

public class CreateOpportunityDto
{
    [Required]
    [StringLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(50)]
    public string Stage { get; set; } = "Prospecting";

    [Range(0, 100)]
    public int Probability { get; set; }

    public DateTime? ExpectedCloseDate { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = "Open";
}
