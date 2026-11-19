
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.Models;

public class FollowUp
{
    public int FollowUpId { get; set; }

    // Related records
    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    public int? OpportunityId { get; set; }

    // Follow-up information
    [Required]
    public DateTime FollowUpDate { get; set; }

    [Required]
    [StringLength(30)]
    public string FollowUpType { get; set; } = "Call";

    [StringLength(2000)]
    public string? Remarks { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Planned";

    // Assignment
    public string? AssignedTo { get; set; }

    // Audit information
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }
}
