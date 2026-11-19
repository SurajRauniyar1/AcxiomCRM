
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Web.DTOs.Activities;

public class UpdateActivityDto
{
    [Required]
    [StringLength(30)]
    public string ActivityType { get; set; } = "Call";

    [Required]
    [StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DateTime ActivityDate { get; set; }

    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = "Planned";
}
