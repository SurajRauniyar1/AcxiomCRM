
namespace AcxiomCRM.Web.ViewModels;

public class DashboardViewModel
{
    // Dashboard date filters
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // Customer statistics
    public int TotalCustomers { get; set; }

    // Lead statistics
    public int TotalLeads { get; set; }
    public int ConvertedLeads { get; set; }
    public double LeadConversionRate { get; set; }

    // Follow-up statistics
    public int TotalFollowUps { get; set; }
    public int FollowUpsDueToday { get; set; }
    public int OverdueFollowUps { get; set; }

    // Opportunity statistics
    public int TotalOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }

    public decimal TotalPipelineValue { get; set; }
    public decimal WonOpportunityValue { get; set; }

    // Dashboard collections
    public List<DashboardActivityItem> RecentActivities { get; set; } = [];
    public List<DashboardAuditItem> RecentAuditLogs { get; set; } = [];
    public List<PipelineStageItem> PipelineStages { get; set; } = [];
    public List<LeadStatusItem> LeadStatusDistribution { get; set; } = [];
    public List<FollowUpStatusItem> FollowUpStatusDistribution { get; set; } = [];

    // Monthly growth chart
    public List<MonthlyGrowthItem> MonthlyGrowth { get; set; } = [];
}

public class DashboardActivityItem
{
    public int ActivityId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
}

public class DashboardAuditItem
{
    public int AuditLogId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string UserEmail { get; set; } = string.Empty;
}

public class PipelineStageItem
{
    public string Stage { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class LeadStatusItem
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class FollowUpStatusItem
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class MonthlyGrowthItem
{
    public string Month { get; set; } = string.Empty;
    public int CustomerCount { get; set; }
    public int LeadCount { get; set; }
}
