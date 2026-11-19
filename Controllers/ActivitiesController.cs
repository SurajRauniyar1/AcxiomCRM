
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ActivityEntity = AcxiomCRM.Web.Models.Activity;

namespace AcxiomCRM.Web.Controllers;

[Authorize]
public class ActivitiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<ActivitiesController> _logger;
    private readonly AuditLogService _auditLogService;

    private static readonly string[] AllowedTypes =
    {
        "Call", "Meeting", "Email", "Task"
    };

    private static readonly string[] AllowedStatuses =
    {
        "Planned", "In Progress", "Completed", "Cancelled"
    };

    public ActivitiesController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        ILogger<ActivitiesController> logger,
        AuditLogService auditLogService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _auditLogService = auditLogService;
    }

    private string CurrentUserId =>
        _userManager.GetUserId(User)!;

    private bool IsAdmin =>
        User.IsInRole("Admin");

    private IQueryable<ActivityEntity> GetAccessibleActivities()
    {
        var query = _context.Activities.AsQueryable();

        if (!IsAdmin)
        {
            var userId = CurrentUserId;
            query = query.Where(a => a.AssignedTo == userId);
        }

        return query;
    }

    private async Task LoadFormDataAsync(
        int? customerId = null,
        int? leadId = null,
        string? assignedTo = null)
    {
        var customersQuery = _context.Customers.AsQueryable();
        var leadsQuery = _context.Leads.AsQueryable();

        if (!IsAdmin)
        {
            var userId = CurrentUserId;

            customersQuery = customersQuery.Where(
                c => c.OwnerId == userId);

            leadsQuery = leadsQuery.Where(
                l => l.AssignedTo == userId);
        }

        ViewBag.Customers = new SelectList(
            await customersQuery
                .OrderBy(c => c.CustomerName)
                .ToListAsync(),
            "CustomerId",
            "CustomerName",
            customerId);

        ViewBag.Leads = new SelectList(
            await leadsQuery
                .OrderBy(l => l.LeadName)
                .ToListAsync(),
            "LeadId",
            "LeadName",
            leadId);

        if (IsAdmin)
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
            var currentUser = await _userManager.FindByIdAsync(
                CurrentUserId);

            ViewBag.AssignedUsers = new SelectList(
                currentUser == null
                    ? new List<IdentityUser>()
                    : new List<IdentityUser> { currentUser },
                "Id",
                "Email",
                CurrentUserId);
        }
    }

    // GET: /Activities
    public async Task<IActionResult> Index(
        string? search,
        string? type,
        string? status,
        int page = 1)
    {
        const int pageSize = 10;

        var query = GetAccessibleActivities();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(a =>
                a.Subject.Contains(search) ||
                (a.Description != null &&
                 a.Description.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(a => a.ActivityType == type);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);

        var totalRecords = await query.CountAsync();

        var totalPages = (int)Math.Ceiling(
            totalRecords / (double)pageSize);

        page = Math.Max(1, Math.Min(page, Math.Max(totalPages, 1)));

        var activities = await query
            .OrderByDescending(a => a.ActivityDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var customerIds = activities
            .Where(a => a.CustomerId.HasValue)
            .Select(a => a.CustomerId!.Value)
            .Distinct()
            .ToList();

        var leadIds = activities
            .Where(a => a.LeadId.HasValue)
            .Select(a => a.LeadId!.Value)
            .Distinct()
            .ToList();

        var userIds = activities
            .Where(a => !string.IsNullOrEmpty(a.AssignedTo))
            .Select(a => a.AssignedTo!)
            .Distinct()
            .ToList();

        ViewBag.CustomerNames = await _context.Customers
            .Where(c => customerIds.Contains(c.CustomerId))
            .ToDictionaryAsync(
                c => c.CustomerId,
                c => c.CustomerName);

        ViewBag.LeadNames = await _context.Leads
            .Where(l => leadIds.Contains(l.LeadId))
            .ToDictionaryAsync(
                l => l.LeadId,
                l => l.LeadName);

        ViewBag.UserNames = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => u.Email ?? u.UserName ?? u.Id);

        ViewBag.TotalRecords = totalRecords;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;

        ViewData["Search"] = search;
        ViewData["Type"] = type;
        ViewData["Status"] = status;

        return View(activities);
    }

    // GET: /Activities/Create
    public async Task<IActionResult> Create()
    {
        await LoadFormDataAsync(
            assignedTo: CurrentUserId);

        return View(new ActivityEntity
        {
            ActivityType = "Call",
            Status = "Planned",
            ActivityDate = DateTime.UtcNow.AddDays(1),
            AssignedTo = CurrentUserId
        });
    }

    // POST: /Activities/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ActivityEntity activity)
    {
        if (!IsAdmin)
        {
            activity.AssignedTo = CurrentUserId;
        }

        await ValidateActivityAsync(activity);

        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync(
                activity.CustomerId,
                activity.LeadId,
                activity.AssignedTo);

            return View(activity);
        }

        try
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            activity.ActivityDate = DateTime.SpecifyKind(
                activity.ActivityDate,
                DateTimeKind.Utc);

            activity.CreatedDate = DateTime.UtcNow;
            activity.CreatedBy = User.Identity?.Name;

            _context.Activities.Add(activity);

            // Save first to generate ActivityId.
            await _context.SaveChangesAsync();

            var newValue = new
            {
                activity.ActivityId,
                activity.ActivityType,
                activity.Subject,
                activity.Description,
                activity.ActivityDate,
                activity.CustomerId,
                activity.LeadId,
                activity.AssignedTo,
                activity.Status
            };

            await _auditLogService.LogAsync(
                "Create",
                "Activity",
                activity.ActivityId.ToString(),
                newValue: newValue);

            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "Activity created successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating activity.");

            ModelState.AddModelError(
                string.Empty,
                "Unable to create activity. Please try again.");

            await LoadFormDataAsync(
                activity.CustomerId,
                activity.LeadId,
                activity.AssignedTo);

            return View(activity);
        }
    }

    // GET: /Activities/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var activity = await GetAccessibleActivities()
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
            return NotFound();

        if (activity.CustomerId.HasValue)
        {
            ViewBag.CustomerName = await _context.Customers
                .Where(c => c.CustomerId == activity.CustomerId)
                .Select(c => c.CustomerName)
                .FirstOrDefaultAsync();
        }

        if (activity.LeadId.HasValue)
        {
            ViewBag.LeadName = await _context.Leads
                .Where(l => l.LeadId == activity.LeadId)
                .Select(l => l.LeadName)
                .FirstOrDefaultAsync();
        }

        if (!string.IsNullOrEmpty(activity.AssignedTo))
        {
            var user = await _userManager.FindByIdAsync(
                activity.AssignedTo);

            ViewBag.AssignedUser =
                user?.Email ?? "Unknown user";
        }

        return View(activity);
    }

    // GET: /Activities/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var activity = await GetAccessibleActivities()
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
            return NotFound();

        await LoadFormDataAsync(
            activity.CustomerId,
            activity.LeadId,
            activity.AssignedTo);

        return View(activity);
    }

    // POST: /Activities/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        ActivityEntity activity)
    {
        if (id != activity.ActivityId)
            return NotFound();

        var existing = await GetAccessibleActivities()
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (existing == null)
            return NotFound();

        // Non-admin users cannot change activity assignment.
        if (!IsAdmin)
        {
            activity.AssignedTo = existing.AssignedTo;
        }

        await ValidateActivityAsync(activity);

        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync(
                activity.CustomerId,
                activity.LeadId,
                activity.AssignedTo);

            return View(activity);
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        // Capture old values before modifying the entity.
        var oldValue = new
        {
            existing.ActivityId,
            existing.ActivityType,
            existing.Subject,
            existing.Description,
            existing.ActivityDate,
            existing.CustomerId,
            existing.LeadId,
            existing.AssignedTo,
            existing.Status
        };

        existing.ActivityType = activity.ActivityType;
        existing.Subject = activity.Subject;
        existing.Description = activity.Description;

        existing.ActivityDate = DateTime.SpecifyKind(
            activity.ActivityDate,
            DateTimeKind.Utc);

        existing.CustomerId = activity.CustomerId;
        existing.LeadId = activity.LeadId;

        if (IsAdmin)
        {
            existing.AssignedTo = activity.AssignedTo;
        }

        existing.Status = activity.Status;
        existing.ModifiedDate = DateTime.UtcNow;

        var newValue = new
        {
            existing.ActivityId,
            existing.ActivityType,
            existing.Subject,
            existing.Description,
            existing.ActivityDate,
            existing.CustomerId,
            existing.LeadId,
            existing.AssignedTo,
            existing.Status
        };

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            "Update",
            "Activity",
            existing.ActivityId.ToString(),
            oldValue,
            newValue);

        await transaction.CommitAsync();

        TempData["SuccessMessage"] =
            "Activity updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Activities/Delete/5
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
            return NotFound();

        return View(activity);
    }

    // POST: /Activities/Delete/5
    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.ActivityId == id);

        if (activity == null)
            return NotFound();

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        var oldValue = new
        {
            activity.ActivityId,
            activity.ActivityType,
            activity.Subject,
            activity.Description,
            activity.ActivityDate,
            activity.CustomerId,
            activity.LeadId,
            activity.AssignedTo,
            activity.Status
        };

        _context.Activities.Remove(activity);

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            "Delete",
            "Activity",
            activity.ActivityId.ToString(),
            oldValue: oldValue);

        await transaction.CommitAsync();

        TempData["SuccessMessage"] =
            "Activity deleted successfully.";

        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateActivityAsync(
        ActivityEntity activity)
    {
        if (!AllowedTypes.Contains(activity.ActivityType))
        {
            ModelState.AddModelError(
                nameof(activity.ActivityType),
                "Select a valid activity type.");
        }

        if (!AllowedStatuses.Contains(activity.Status))
        {
            ModelState.AddModelError(
                nameof(activity.Status),
                "Select a valid activity status.");
        }

        bool hasCustomer = activity.CustomerId.HasValue;
        bool hasLead = activity.LeadId.HasValue;

        if (hasCustomer == hasLead)
        {
            ModelState.AddModelError(
                string.Empty,
                "Select exactly one Customer or Lead.");
        }

        if (hasCustomer)
        {
            var customerQuery = _context.Customers
                .Where(c => c.CustomerId == activity.CustomerId);

            if (!IsAdmin)
            {
                var userId = CurrentUserId;

                customerQuery = customerQuery.Where(
                    c => c.OwnerId == userId);
            }

            if (!await customerQuery.AnyAsync())
            {
                ModelState.AddModelError(
                    nameof(activity.CustomerId),
                    "The selected customer does not exist or you do not have access to it.");
            }
        }

        if (hasLead)
        {
            var leadQuery = _context.Leads
                .Where(l => l.LeadId == activity.LeadId);

            if (!IsAdmin)
            {
                var userId = CurrentUserId;

                leadQuery = leadQuery.Where(
                    l => l.AssignedTo == userId);
            }

            if (!await leadQuery.AnyAsync())
            {
                ModelState.AddModelError(
                    nameof(activity.LeadId),
                    "The selected lead does not exist or you do not have access to it.");
            }
        }

        if (!string.IsNullOrWhiteSpace(activity.AssignedTo))
        {
            var userExists = await _userManager.Users.AnyAsync(
                u => u.Id == activity.AssignedTo);

            if (!userExists)
            {
                ModelState.AddModelError(
                    nameof(activity.AssignedTo),
                    "The selected user does not exist.");
            }
        }
        else if (IsAdmin)
        {
            ModelState.AddModelError(
                nameof(activity.AssignedTo),
                "Select a user to assign this activity to.");
        }
    }
}
