
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.Models;

public class Activity
{
    public int ActivityId { get; set; }

    [Required]
    [StringLength(30)]
    public string ActivityType { get; set; } = "Call";

    [Required]
    [StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

    // An activity can belong to a Customer or a Lead.
    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    public string? AssignedTo { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Planned";

    // Audit information
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }
}
