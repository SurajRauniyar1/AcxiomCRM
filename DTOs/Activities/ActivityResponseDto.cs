
namespace AcxiomCRM.Web.DTOs.Activities;

public class ActivityResponseDto
{
    public int ActivityId { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime ActivityDate { get; set; }

    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    public string? AssignedTo { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
}
