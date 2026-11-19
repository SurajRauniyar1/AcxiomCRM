
namespace AcxiomCRM.Web.DTOs.Opportunities;

public class OpportunityResponseDto
{
    public int OpportunityId { get; set; }

    public string OpportunityName { get; set; } = string.Empty;

    public int? CustomerId { get; set; }

    public int? LeadId { get; set; }

    public decimal Amount { get; set; }

    public string Stage { get; set; } = string.Empty;

    public int Probability { get; set; }

    public DateTime? ExpectedCloseDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal WeightedValue { get; set; }

    public DateTime CreatedDate { get; set; }
}
