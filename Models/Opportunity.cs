
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.Models;

public class Opportunity
{
    public int OpportunityId { get; set; }

    [Required]
    [StringLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    // Related Customer or Lead
    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    // Deal information
    [Range(0.01, 999999999999)]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(30)]
    public string Stage { get; set; } = "Qualification";

    [Range(0, 100)]
    public int Probability { get; set; } = 10;

    public DateTime ExpectedCloseDate { get; set; } =
        DateTime.UtcNow.AddDays(30);

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Open";

    // Assignment
    public string? AssignedTo { get; set; }

    // Audit information
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }

    // Calculated weighted pipeline value
    public decimal WeightedValue =>
        Amount * Probability / 100m;
}
