
using System.Security.Claims;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public HomeController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: Home / Dashboard
    public async Task<IActionResult> Index(
        DateTime? startDate,
        DateTime? endDate)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        var isAdmin = User.IsInRole("Admin");

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // =====================================
        // DATE FILTERS - DEFAULT CURRENT MONTH
        // =====================================

        var selectedStartDate = startDate.HasValue
            ? DateTime.SpecifyKind(
                startDate.Value.Date,
                DateTimeKind.Utc)
            : new DateTime(
                today.Year,
                today.Month,
                1,
                0, 0, 0,
                DateTimeKind.Utc);

        var selectedEndDate = endDate.HasValue
            ? DateTime.SpecifyKind(
                endDate.Value.Date,
                DateTimeKind.Utc)
            : new DateTime(
                today.Year,
                today.Month,
                1,
                0, 0, 0,
                DateTimeKind.Utc).AddMonths(1).AddDays(-1);

        // Validate date range
        if (selectedStartDate > selectedEndDate)
        {
            ModelState.AddModelError(
                string.Empty,
                "Start date cannot be after end date.");

            selectedStartDate = new DateTime(
                today.Year,
                today.Month,
                1,
                0, 0, 0,
                DateTimeKind.Utc);

            selectedEndDate = today;
        }

        // End date is inclusive for users.
        // Database comparison uses an exclusive upper bound.
        var endExclusive = selectedEndDate.AddDays(1);

        // =====================================
        // BASE QUERIES
        // =====================================

        var customersQuery =
            _context.Customers.AsNoTracking();

        var leadsQuery =
            _context.Leads.AsNoTracking();

        var followUpsQuery =
            _context.FollowUps.AsNoTracking();

        var opportunitiesQuery =
            _context.Opportunities.AsNoTracking();

        var activitiesQuery =
            _context.Activities.AsNoTracking();

        // =====================================
        // ROLE-BASED ACCESS
        // =====================================

        if (!isAdmin)
        {
            customersQuery = customersQuery
                .Where(c => c.OwnerId == userId);

            leadsQuery = leadsQuery
                .Where(l => l.AssignedTo == userId);

            followUpsQuery = followUpsQuery
                .Where(f => f.AssignedTo == userId);

            opportunitiesQuery = opportunitiesQuery
                .Where(o => o.AssignedTo == userId);

            activitiesQuery = activitiesQuery
                .Where(a => a.AssignedTo == userId);
        }

        // =====================================
        // APPLY DATE FILTERS
        // =====================================

        customersQuery = customersQuery
            .Where(c =>
                c.CreatedDate >= selectedStartDate &&
                c.CreatedDate < endExclusive);

        leadsQuery = leadsQuery
            .Where(l =>
                l.CreatedDate >= selectedStartDate &&
                l.CreatedDate < endExclusive);

        followUpsQuery = followUpsQuery
            .Where(f =>
                f.FollowUpDate >= selectedStartDate &&
                f.FollowUpDate < endExclusive);

        opportunitiesQuery = opportunitiesQuery
            .Where(o =>
                o.CreatedDate >= selectedStartDate &&
                o.CreatedDate < endExclusive);

        activitiesQuery = activitiesQuery
            .Where(a =>
                a.ActivityDate >= selectedStartDate &&
                a.ActivityDate < endExclusive);

        var dashboard = new DashboardViewModel
        {
            StartDate = selectedStartDate,
            EndDate = selectedEndDate
        };

        // =====================================
        // CUSTOMER STATISTICS
        // =====================================

        dashboard.TotalCustomers =
            await customersQuery.CountAsync();

        // =====================================
        // LEAD STATISTICS
        // =====================================

        dashboard.TotalLeads =
            await leadsQuery.CountAsync();

        dashboard.ConvertedLeads =
            await leadsQuery.CountAsync(
                l => l.IsConverted);

        dashboard.LeadConversionRate =
            dashboard.TotalLeads == 0
                ? 0
                : Math.Round(
                    dashboard.ConvertedLeads * 100.0 /
                    dashboard.TotalLeads, 2);

        // LEAD STATUS DISTRIBUTION

        dashboard.LeadStatusDistribution =
            await leadsQuery
                .GroupBy(l => l.Status)
                .Select(g => new LeadStatusItem
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .OrderBy(g => g.Status)
                .ToListAsync();

        // =====================================
        // FOLLOW-UP STATISTICS
        // =====================================

        dashboard.TotalFollowUps =
            await followUpsQuery.CountAsync();

        // Current workload indicators are independent
        // of the selected historical date range.

        var workloadFollowUps =
            _context.FollowUps.AsNoTracking();

        if (!isAdmin)
        {
            workloadFollowUps = workloadFollowUps
                .Where(f => f.AssignedTo == userId);
        }

        dashboard.FollowUpsDueToday =
            await workloadFollowUps.CountAsync(f =>
                f.FollowUpDate >= today &&
                f.FollowUpDate < tomorrow &&
                f.Status != "Completed" &&
                f.Status != "Cancelled");

        dashboard.OverdueFollowUps =
            await workloadFollowUps.CountAsync(f =>
                f.FollowUpDate < today &&
                f.Status != "Completed" &&
                f.Status != "Cancelled");

        // FOLLOW-UP STATUS DISTRIBUTION

        dashboard.FollowUpStatusDistribution =
            await followUpsQuery
                .GroupBy(f => f.Status)
                .Select(g => new FollowUpStatusItem
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .OrderBy(g => g.Status)
                .ToListAsync();

        // =====================================
        // OPPORTUNITY STATISTICS
        // =====================================

        dashboard.TotalOpportunities =
            await opportunitiesQuery.CountAsync();

        dashboard.WonOpportunities =
            await opportunitiesQuery.CountAsync(
                o => o.Stage == "Won");

        dashboard.LostOpportunities =
            await opportunitiesQuery.CountAsync(
                o => o.Stage == "Lost");

        dashboard.TotalPipelineValue =
            await opportunitiesQuery
                .Where(o =>
                    o.Stage != "Won" &&
                    o.Stage != "Lost")
                .SumAsync(o => (decimal?)o.Amount) ?? 0;

        dashboard.WonOpportunityValue =
            await opportunitiesQuery
                .Where(o => o.Stage == "Won")
                .SumAsync(o => (decimal?)o.Amount) ?? 0;

        // =====================================
        // OPPORTUNITY PIPELINE
        // =====================================

        dashboard.PipelineStages =
            await opportunitiesQuery
                .GroupBy(o => o.Stage)
                .Select(g => new PipelineStageItem
                {
                    Stage = g.Key,
                    Count = g.Count(),
                    Amount = g.Sum(o => o.Amount)
                })
                .OrderBy(g => g.Stage)
                .ToListAsync();

        // =====================================
        // MONTHLY CUSTOMER AND LEAD GROWTH
        // SELECTED DATE RANGE
        // =====================================

        var customerDates = await customersQuery
            .Select(c => c.CreatedDate)
            .ToListAsync();

        var leadDates = await leadsQuery
            .Select(l => l.CreatedDate)
            .ToListAsync();

        var firstMonth = new DateTime(
            selectedStartDate.Year,
            selectedStartDate.Month,
            1,
            0, 0, 0,
            DateTimeKind.Utc);

        var lastMonth = new DateTime(
            selectedEndDate.Year,
            selectedEndDate.Month,
            1,
            0, 0, 0,
            DateTimeKind.Utc);

        var totalMonths =
            ((lastMonth.Year - firstMonth.Year) * 12)
            + lastMonth.Month
            - firstMonth.Month + 1;

        dashboard.MonthlyGrowth =
            Enumerable.Range(0, totalMonths)
                .Select(i =>
                {
                    var month = firstMonth.AddMonths(i);

                    return new MonthlyGrowthItem
                    {
                        Month = month.ToString("MMM yyyy"),

                        CustomerCount = customerDates.Count(d =>
                            d.Year == month.Year &&
                            d.Month == month.Month),

                        LeadCount = leadDates.Count(d =>
                            d.Year == month.Year &&
                            d.Month == month.Month)
                    };
                })
                .ToList();

        // =====================================
        // RECENT ACTIVITIES
        // =====================================

        dashboard.RecentActivities =
            await activitiesQuery
                .OrderByDescending(a => a.ActivityDate)
                .Take(5)
                .Select(a => new DashboardActivityItem
                {
                    ActivityId = a.ActivityId,
                    Subject = a.Subject,
                    ActivityType = a.ActivityType,
                    Status = a.Status,
                    ActivityDate = a.ActivityDate
                })
                .ToListAsync();

        // =====================================
        // RECENT AUDIT EVENTS - ADMIN ONLY
        // =====================================

        if (isAdmin)
        {
            var recentLogs = await _context.AuditLogs
                .AsNoTracking()
                .Where(a =>
                    a.CreatedDate >= selectedStartDate &&
                    a.CreatedDate < endExclusive)
                .OrderByDescending(a => a.CreatedDate)
                .Take(5)
                .ToListAsync();

            var auditUserIds = recentLogs
                .Where(a => !string.IsNullOrEmpty(a.UserId))
                .Select(a => a.UserId!)
                .Distinct()
                .ToList();

            var userEmails = await _userManager.Users
                .Where(u => auditUserIds.Contains(u.Id))
                .ToDictionaryAsync(
                    u => u.Id,
                    u => u.Email ?? u.UserName ?? u.Id);

            dashboard.RecentAuditLogs = recentLogs
                .Select(a => new DashboardAuditItem
                {
                    AuditLogId = a.AuditLogId,
                    Action = a.Action,
                    EntityName = a.EntityName,
                    RecordId = a.RecordId,
                    CreatedDate = a.CreatedDate,

                    UserEmail =
                        a.UserId != null &&
                        userEmails.TryGetValue(
                            a.UserId, out var email)
                            ? email
                            : "Unknown User"
                })
                .ToList();
        }

        return View(dashboard);
    }

    // GET: Home/Privacy
    public IActionResult Privacy()
    {
        return View();
    }

    // GET: Home/Error
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = HttpContext.TraceIdentifier
        });
    }
}
