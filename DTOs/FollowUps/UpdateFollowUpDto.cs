
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.DTOs.FollowUps;

public class UpdateFollowUpDto
{
    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    public int? OpportunityId { get; set; }

    [Required]
    public DateTime FollowUpDate { get; set; }

    [Required]
    [StringLength(30)]
    public string FollowUpType { get; set; } = "Call";

    public string? Remarks { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Planned";
}
