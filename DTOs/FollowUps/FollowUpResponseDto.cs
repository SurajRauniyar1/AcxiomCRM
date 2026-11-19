
namespace AcxiomCRM.Web.DTOs.FollowUps;

public class FollowUpResponseDto
{
    public int FollowUpId { get; set; }

    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    public int? OpportunityId { get; set; }

    public DateTime FollowUpDate { get; set; }

    public string FollowUpType { get; set; } = string.Empty;

    public string? Remarks { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
}
