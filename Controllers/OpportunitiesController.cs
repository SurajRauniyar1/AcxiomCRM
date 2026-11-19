
using System.Security.Claims;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Controllers;

[Authorize]
public class OpportunitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<OpportunitiesController> _logger;
    private readonly AuditLogService _auditLogService;

    private const int PageSize = 10;

    private static readonly string[] AllowedStages =
    {
        "Qualification", "Proposal", "Negotiation", "Won", "Lost"
    };

    private static readonly string[] AllowedStatuses =
    {
        "Open", "Won", "Lost", "Closed"
    };

    public OpportunitiesController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        ILogger<OpportunitiesController> logger,
        AuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _auditLogService = auditLogService;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("User ID not found.");

    // Admin sees all opportunities.
    // Manager and SalesExecutive see assigned opportunities.
    private IQueryable<Opportunity> GetAccessibleOpportunities()
    {
        var query = _context.Opportunities.AsQueryable();

        if (User.IsInRole("Admin"))
            return query;

        return query.Where(o => o.AssignedTo == CurrentUserId);
    }

    // Load only records the current user is allowed to associate.
    private async Task LoadFormDataAsync(
        int? customerId = null,
        int? leadId = null,
        string? assignedTo = null)
    {
        var customers = _context.Customers.AsQueryable();
        var leads = _context.Leads.AsQueryable();

        if (!User.IsInRole("Admin"))
        {
            customers = customers.Where(c =>
                c.OwnerId == CurrentUserId);

            leads = leads.Where(l =>
                l.AssignedTo == CurrentUserId);
        }

        ViewBag.Customers = new SelectList(
            await customers
                .OrderBy(c => c.CustomerName)
                .ToListAsync(),
            "CustomerId",
            "CustomerName",
            customerId);

        ViewBag.Leads = new SelectList(
            await leads
                .OrderBy(l => l.LeadName)
                .ToListAsync(),
            "LeadId",
            "LeadName",
            leadId);

        if (User.IsInRole("Admin"))
        {
            ViewBag.AssignedUsers = new SelectList(
                await _userManager.Users
                    .OrderBy(u => u.Email)
                    .ToListAsync(),
                "Id",
                "Email",
                assignedTo);
        }
        else
        {
            ViewBag.AssignedUsers = new SelectList(
                new[]
                {
                    new
                    {
                        Id = CurrentUserId,
                        Email = User.Identity?.Name ?? "Current User"
                    }
                },
                "Id",
                "Email",
                CurrentUserId);
        }
    }

    // GET: /Opportunities
    public async Task<IActionResult> Index(
        string? search,
        string? stage,
        string? status,
        int page = 1)
    {
        var query = GetAccessibleOpportunities();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(o =>
                o.OpportunityName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(stage))
            query = query.Where(o => o.Stage == stage);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);

        var totalRecords = await query.CountAsync();

        var totalPages = (int)Math.Ceiling(
            totalRecords / (double)PageSize);

        page = Math.Max(1, Math.Min(page, Math.Max(totalPages, 1)));

        var opportunities = await query
            .OrderByDescending(o => o.CreatedDate)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var customerIds = opportunities
            .Where(o => o.CustomerId.HasValue)
            .Select(o => o.CustomerId!.Value)
            .Distinct()
            .ToList();

        var leadIds = opportunities
            .Where(o => o.LeadId.HasValue)
            .Select(o => o.LeadId!.Value)
            .Distinct()
            .ToList();

        var userIds = opportunities
            .Where(o => !string.IsNullOrEmpty(o.AssignedTo))
            .Select(o => o.AssignedTo!)
            .Distinct()
            .ToList();

        ViewBag.CustomerNames = await _context.Customers
            .Where(c => customerIds.Contains(c.CustomerId))
            .ToDictionaryAsync(c => c.CustomerId, c => c.CustomerName);

        ViewBag.LeadNames = await _context.Leads
            .Where(l => leadIds.Contains(l.LeadId))
            .ToDictionaryAsync(l => l.LeadId, l => l.LeadName);

        ViewBag.UserNames = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => u.Email ?? u.UserName ?? u.Id);

        // Pipeline totals respect the current user's access.
        var pipelineQuery = GetAccessibleOpportunities();

        ViewBag.TotalPipeline = await pipelineQuery
            .Where(o => o.Status == "Open")
            .SumAsync(o => (decimal?)o.Amount) ?? 0m;

        ViewBag.WeightedPipeline = await pipelineQuery
            .Where(o => o.Status == "Open")
            .SumAsync(o =>
                (decimal?)(o.Amount * o.Probability / 100m)) ?? 0m;

        ViewBag.TotalWon = await pipelineQuery
            .Where(o => o.Status == "Won")
            .SumAsync(o => (decimal?)o.Amount) ?? 0m;

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalRecords = totalRecords;

        ViewData["Search"] = search;
        ViewData["Stage"] = stage;
        ViewData["Status"] = status;

        return View(opportunities);
    }

    // GET: /Opportunities/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var opportunity = await GetAccessibleOpportunities()
            .FirstOrDefaultAsync(o => o.OpportunityId == id);

        if (opportunity == null)
            return NotFound();

        if (opportunity.CustomerId.HasValue)
        {
            ViewBag.CustomerName = await _context.Customers
                .Where(c => c.CustomerId == opportunity.CustomerId)
                .Select(c => c.CustomerName)
                .FirstOrDefaultAsync();
        }

        if (opportunity.LeadId.HasValue)
        {
            ViewBag.LeadName = await _context.Leads
                .Where(l => l.LeadId == opportunity.LeadId)
                .Select(l => l.LeadName)
                .FirstOrDefaultAsync();
        }

        if (!string.IsNullOrEmpty(opportunity.AssignedTo))
        {
            var user = await _userManager.FindByIdAsync(
                opportunity.AssignedTo);

            ViewBag.AssignedUser = user?.Email ?? "Unknown user";
        }

        return View(opportunity);
    }

    // GET: /Opportunities/Create
    public async Task<IActionResult> Create()
    {
        await LoadFormDataAsync(
            assignedTo: User.IsInRole("Admin") ? null : CurrentUserId);

        return View(new Opportunity
        {
            Stage = "Qualification",
            Status = "Open",
            Probability = 10,
            AssignedTo = User.IsInRole("Admin") ? null : CurrentUserId,
            ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
        });
    }

    // POST: /Opportunities/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Opportunity opportunity)
    {
        ModelState.Remove(nameof(Opportunity.CreatedDate));
        ModelState.Remove(nameof(Opportunity.CreatedBy));
        ModelState.Remove(nameof(Opportunity.ModifiedDate));

        // Non-admin users can only assign opportunities to themselves.
        if (!User.IsInRole("Admin"))
        {
            ModelState.Remove(nameof(Opportunity.AssignedTo));
            opportunity.AssignedTo = CurrentUserId;
        }
        else if (string.IsNullOrWhiteSpace(opportunity.AssignedTo))
        {
            ModelState.AddModelError(
                nameof(opportunity.AssignedTo),
                "Please select a user.");
        }

        await ValidateOpportunityAsync(opportunity);

        if (opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date &&
            opportunity.Status == "Open")
        {
            ModelState.AddModelError(
                nameof(opportunity.ExpectedCloseDate),
                "Expected close date cannot be in the past for an open opportunity.");
        }

        if (!string.IsNullOrEmpty(opportunity.AssignedTo) &&
            await _userManager.FindByIdAsync(opportunity.AssignedTo) == null)
        {
            ModelState.AddModelError(
                nameof(opportunity.AssignedTo),
                "Please select a valid user.");
        }

        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync(
                opportunity.CustomerId,
                opportunity.LeadId,
                opportunity.AssignedTo);

            return View(opportunity);
        }

        try
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            opportunity.ExpectedCloseDate = DateTime.SpecifyKind(
                opportunity.ExpectedCloseDate,
                DateTimeKind.Utc);

            opportunity.CreatedDate = DateTime.UtcNow;
            opportunity.CreatedBy = User.Identity?.Name;
            opportunity.ModifiedDate = null;

            _context.Opportunities.Add(opportunity);

            // Save first to generate OpportunityId.
            await _context.SaveChangesAsync();

            var newValue = new
            {
                opportunity.OpportunityId,
                opportunity.OpportunityName,
                opportunity.CustomerId,
                opportunity.LeadId,
                opportunity.Amount,
                opportunity.Stage,
                opportunity.Probability,
                opportunity.ExpectedCloseDate,
                opportunity.Status,
                opportunity.AssignedTo
            };

            await _auditLogService.LogAsync(
                "Create",
                "Opportunity",
                opportunity.OpportunityId.ToString(),
                newValue: newValue);

            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "Opportunity created successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating opportunity.");

            ModelState.AddModelError(
                string.Empty,
                "Unable to create opportunity. Please try again.");

            await LoadFormDataAsync(
                opportunity.CustomerId,
                opportunity.LeadId,
                opportunity.AssignedTo);

            return View(opportunity);
        }
    }

    // GET: /Opportunities/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var opportunity = await GetAccessibleOpportunities()
            .FirstOrDefaultAsync(o => o.OpportunityId == id);

        if (opportunity == null)
            return NotFound();

        await LoadFormDataAsync(
            opportunity.CustomerId,
            opportunity.LeadId,
            opportunity.AssignedTo);

        return View(opportunity);
    }

    // POST: /Opportunities/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        Opportunity opportunity)
    {
        if (id != opportunity.OpportunityId)
            return NotFound();

        var existing = await GetAccessibleOpportunities()
            .FirstOrDefaultAsync(o => o.OpportunityId == id);

        if (existing == null)
            return NotFound();

        ModelState.Remove(nameof(Opportunity.CreatedDate));
        ModelState.Remove(nameof(Opportunity.CreatedBy));
        ModelState.Remove(nameof(Opportunity.ModifiedDate));

        // Only Admin can reassign an opportunity.
        if (!User.IsInRole("Admin"))
        {
            ModelState.Remove(nameof(Opportunity.AssignedTo));
            opportunity.AssignedTo = existing.AssignedTo;
        }

        await ValidateOpportunityAsync(opportunity);

        if (opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date &&
            opportunity.Status == "Open")
        {
            ModelState.AddModelError(
                nameof(opportunity.ExpectedCloseDate),
                "Expected close date cannot be in the past for an open opportunity.");
        }

        if (User.IsInRole("Admin") &&
            !string.IsNullOrEmpty(opportunity.AssignedTo) &&
            await _userManager.FindByIdAsync(opportunity.AssignedTo) == null)
        {
            ModelState.AddModelError(
                nameof(opportunity.AssignedTo),
                "Please select a valid user.");
        }

        if (!ModelState.IsValid)
        {
            opportunity.CreatedDate = existing.CreatedDate;
            opportunity.CreatedBy = existing.CreatedBy;

            await LoadFormDataAsync(
                opportunity.CustomerId,
                opportunity.LeadId,
                existing.AssignedTo);

            return View(opportunity);
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        // Capture old values before modifying the entity.
        var oldValue = new
        {
            existing.OpportunityId,
            existing.OpportunityName,
            existing.CustomerId,
            existing.LeadId,
            existing.Amount,
            existing.Stage,
            existing.Probability,
            existing.ExpectedCloseDate,
            existing.Status,
            existing.AssignedTo
        };

        existing.OpportunityName = opportunity.OpportunityName;
        existing.CustomerId = opportunity.CustomerId;
        existing.LeadId = opportunity.LeadId;
        existing.Amount = opportunity.Amount;
        existing.Stage = opportunity.Stage;
        existing.Probability = opportunity.Probability;

        existing.ExpectedCloseDate = DateTime.SpecifyKind(
            opportunity.ExpectedCloseDate,
            DateTimeKind.Utc);

        existing.Status = opportunity.Status;

        if (User.IsInRole("Admin"))
            existing.AssignedTo = opportunity.AssignedTo;

        existing.ModifiedDate = DateTime.UtcNow;

        var newValue = new
        {
            existing.OpportunityId,
            existing.OpportunityName,
            existing.CustomerId,
            existing.LeadId,
            existing.Amount,
            existing.Stage,
            existing.Probability,
            existing.ExpectedCloseDate,
            existing.Status,
            existing.AssignedTo
        };

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            "Update",
            "Opportunity",
            existing.OpportunityId.ToString(),
            oldValue,
            newValue);

        await transaction.CommitAsync();

        TempData["SuccessMessage"] =
            "Opportunity updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Opportunities/Delete/5
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var opportunity = await GetAccessibleOpportunities()
            .FirstOrDefaultAsync(o => o.OpportunityId == id);

        if (opportunity == null)
            return NotFound();

        return View(opportunity);
    }

    // POST: /Opportunities/Delete/5
    [Authorize(Roles = "Admin")]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var opportunity = await GetAccessibleOpportunities()
            .FirstOrDefaultAsync(o => o.OpportunityId == id);

        if (opportunity == null)
            return NotFound();

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        var oldValue = new
        {
            opportunity.OpportunityId,
            opportunity.OpportunityName,
            opportunity.CustomerId,
            opportunity.LeadId,
            opportunity.Amount,
            opportunity.Stage,
            opportunity.Probability,
            opportunity.ExpectedCloseDate,
            opportunity.Status,
            opportunity.AssignedTo
        };

        _context.Opportunities.Remove(opportunity);

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            "Delete",
            "Opportunity",
            opportunity.OpportunityId.ToString(),
            oldValue: oldValue);

        await transaction.CommitAsync();

        TempData["SuccessMessage"] =
            "Opportunity deleted successfully.";

        return RedirectToAction(nameof(Index));
    }

    // Validate fields and ensure related records are accessible.
    private async Task ValidateOpportunityAsync(Opportunity opportunity)
    {
        if (!AllowedStages.Contains(opportunity.Stage))
        {
            ModelState.AddModelError(
                nameof(opportunity.Stage),
                "Select a valid opportunity stage.");
        }

        if (!AllowedStatuses.Contains(opportunity.Status))
        {
            ModelState.AddModelError(
                nameof(opportunity.Status),
                "Select a valid opportunity status.");
        }

        if (opportunity.Amount <= 0 &&
            opportunity.Status == "Open")
        {
            ModelState.AddModelError(
                nameof(opportunity.Amount),
                "Amount must be greater than zero for an open opportunity.");
        }

        if (opportunity.Probability < 0 ||
            opportunity.Probability > 100)
        {
            ModelState.AddModelError(
                nameof(opportunity.Probability),
                "Probability must be between 0 and 100.");
        }

        bool hasCustomer = opportunity.CustomerId.HasValue;
        bool hasLead = opportunity.LeadId.HasValue;

        if (hasCustomer == hasLead)
        {
            ModelState.AddModelError(
                string.Empty,
                "Select exactly one Customer or Lead.");

            return;
        }

        if (hasCustomer)
        {
            var customerQuery = _context.Customers
                .Where(c => c.CustomerId == opportunity.CustomerId);

            if (!User.IsInRole("Admin"))
            {
                customerQuery = customerQuery.Where(c =>
                    c.OwnerId == CurrentUserId);
            }

            if (!await customerQuery.AnyAsync())
            {
                ModelState.AddModelError(
                    nameof(opportunity.CustomerId),
                    "The selected customer does not exist or you do not have access to it.");
            }
        }

        if (hasLead)
        {
            var leadQuery = _context.Leads
                .Where(l => l.LeadId == opportunity.LeadId);

            if (!User.IsInRole("Admin"))
            {
                leadQuery = leadQuery.Where(l =>
                    l.AssignedTo == CurrentUserId);
            }

            if (!await leadQuery.AnyAsync())
            {
                ModelState.AddModelError(
                    nameof(opportunity.LeadId),
                    "The selected lead does not exist or you do not have access to it.");
            }
        }

        if (opportunity.Status == "Won" &&
            opportunity.Stage != "Won")
        {
            ModelState.AddModelError(
                nameof(opportunity.Stage),
                "A Won opportunity must have the Won stage.");
        }

        if (opportunity.Status == "Lost" &&
            opportunity.Stage != "Lost")
        {
            ModelState.AddModelError(
                nameof(opportunity.Stage),
                "A Lost opportunity must have the Lost stage.");
        }
    }
}
