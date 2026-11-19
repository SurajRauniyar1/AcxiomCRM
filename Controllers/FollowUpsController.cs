
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
public class FollowUpsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<FollowUpsController> _logger;
    private readonly AuditLogService _auditLogService;

    private const int PageSize = 10;

    private static readonly string[] AllowedStatuses =
    {
        "Planned", "Completed", "Missed", "Cancelled"
    };

    private static readonly string[] AllowedTypes =
    {
        "Call", "Meeting", "Email", "Task"
    };

    public FollowUpsController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        ILogger<FollowUpsController> logger,
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

    // Admin can see all follow-ups.
    // Other users can see only their assigned follow-ups.
    private IQueryable<FollowUp> GetAccessibleFollowUps()
    {
        var query = _context.FollowUps.AsQueryable();

        if (User.IsInRole("Admin"))
            return query;

        return query.Where(f => f.AssignedTo == CurrentUserId);
    }

    // Load dropdown data according to the current user's permissions.
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

    // GET: /FollowUps
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        int page = 1)
    {
        var query = GetAccessibleFollowUps();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(f =>
                (f.Remarks != null && f.Remarks.Contains(search)) ||
                f.FollowUpType.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        var totalRecords = await query.CountAsync();

        var totalPages = (int)Math.Ceiling(
            totalRecords / (double)PageSize);

        page = Math.Max(1, Math.Min(page, Math.Max(totalPages, 1)));

        var followUps = await query
            .OrderBy(f => f.FollowUpDate)
            .ThenBy(f => f.FollowUpId)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var customerIds = followUps
            .Where(f => f.CustomerId.HasValue)
            .Select(f => f.CustomerId!.Value)
            .Distinct()
            .ToList();

        var leadIds = followUps
            .Where(f => f.LeadId.HasValue)
            .Select(f => f.LeadId!.Value)
            .Distinct()
            .ToList();

        var userIds = followUps
            .Where(f => !string.IsNullOrEmpty(f.AssignedTo))
            .Select(f => f.AssignedTo!)
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

        ViewData["Search"] = search;
        ViewData["Status"] = status;

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalRecords = totalRecords;

        return View(followUps);
    }

    // GET: /FollowUps/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var followUp = await GetAccessibleFollowUps()
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (followUp == null)
            return NotFound();

        if (followUp.CustomerId.HasValue)
        {
            ViewBag.CustomerName = await _context.Customers
                .Where(c => c.CustomerId == followUp.CustomerId)
                .Select(c => c.CustomerName)
                .FirstOrDefaultAsync();
        }

        if (followUp.LeadId.HasValue)
        {
            ViewBag.LeadName = await _context.Leads
                .Where(l => l.LeadId == followUp.LeadId)
                .Select(l => l.LeadName)
                .FirstOrDefaultAsync();
        }

        if (!string.IsNullOrEmpty(followUp.AssignedTo))
        {
            var user = await _userManager.FindByIdAsync(
                followUp.AssignedTo);

            ViewBag.AssignedUser = user?.Email ?? "Unknown user";
        }

        return View(followUp);
    }

    // GET: /FollowUps/Create
    public async Task<IActionResult> Create()
    {
        await LoadFormDataAsync(
            assignedTo: User.IsInRole("Admin") ? null : CurrentUserId);

        return View(new FollowUp
        {
            FollowUpDate = DateTime.UtcNow.AddDays(1),
            FollowUpType = "Call",
            Status = "Planned",
            AssignedTo = User.IsInRole("Admin") ? null : CurrentUserId
        });
    }

    // POST: /FollowUps/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUp followUp)
    {
        ModelState.Remove(nameof(FollowUp.CreatedDate));
        ModelState.Remove(nameof(FollowUp.CreatedBy));
        ModelState.Remove(nameof(FollowUp.ModifiedDate));

        // Non-admin users cannot assign follow-ups to other users.
        if (!User.IsInRole("Admin"))
        {
            ModelState.Remove(nameof(FollowUp.AssignedTo));
            followUp.AssignedTo = CurrentUserId;
        }
        else if (string.IsNullOrWhiteSpace(followUp.AssignedTo))
        {
            ModelState.AddModelError(
                nameof(FollowUp.AssignedTo),
                "Please select a user.");
        }

        await ValidateFollowUpAsync(followUp);

        if (followUp.FollowUpDate < DateTime.UtcNow)
        {
            ModelState.AddModelError(
                nameof(followUp.FollowUpDate),
                "A new follow-up must be scheduled for the future.");
        }

        if (!string.IsNullOrEmpty(followUp.AssignedTo) &&
            await _userManager.FindByIdAsync(followUp.AssignedTo) == null)
        {
            ModelState.AddModelError(
                nameof(followUp.AssignedTo),
                "Please select a valid user.");
        }

        if (!ModelState.IsValid)
        {
            await LoadFormDataAsync(
                followUp.CustomerId,
                followUp.LeadId,
                followUp.AssignedTo);

            return View(followUp);
        }

        try
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            followUp.FollowUpDate = DateTime.SpecifyKind(
                followUp.FollowUpDate,
                DateTimeKind.Utc);

            followUp.CreatedDate = DateTime.UtcNow;
            followUp.CreatedBy = User.Identity?.Name;
            followUp.ModifiedDate = null;
            followUp.Status = "Planned";

            _context.FollowUps.Add(followUp);

            // Save first to generate FollowUpId.
            await _context.SaveChangesAsync();

            var newValue = new
            {
                followUp.FollowUpId,
                followUp.CustomerId,
                followUp.LeadId,
                followUp.FollowUpDate,
                followUp.FollowUpType,
                followUp.Remarks,
                followUp.Status,
                followUp.AssignedTo
            };

            await _auditLogService.LogAsync(
                "Create",
                "FollowUp",
                followUp.FollowUpId.ToString(),
                newValue: newValue);

            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "Follow-up scheduled successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating follow-up.");

            ModelState.AddModelError(
                string.Empty,
                "Unable to create follow-up. Please try again.");

            await LoadFormDataAsync(
                followUp.CustomerId,
                followUp.LeadId,
                followUp.AssignedTo);

            return View(followUp);
        }
    }

    // GET: /FollowUps/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var followUp = await GetAccessibleFollowUps()
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (followUp == null)
            return NotFound();

        await LoadFormDataAsync(
            followUp.CustomerId,
            followUp.LeadId,
            followUp.AssignedTo);

        return View(followUp);
    }

    // POST: /FollowUps/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        FollowUp followUp)
    {
        if (id != followUp.FollowUpId)
            return NotFound();

        var existing = await GetAccessibleFollowUps()
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (existing == null)
            return NotFound();

        ModelState.Remove(nameof(FollowUp.CreatedDate));
        ModelState.Remove(nameof(FollowUp.CreatedBy));
        ModelState.Remove(nameof(FollowUp.ModifiedDate));

        // Non-admin users cannot reassign the follow-up.
        if (!User.IsInRole("Admin"))
        {
            ModelState.Remove(nameof(FollowUp.AssignedTo));
            followUp.AssignedTo = existing.AssignedTo;
        }

        await ValidateFollowUpAsync(followUp);

        if (!string.IsNullOrEmpty(followUp.AssignedTo) &&
            await _userManager.FindByIdAsync(followUp.AssignedTo) == null)
        {
            ModelState.AddModelError(
                nameof(followUp.AssignedTo),
                "Please select a valid user.");
        }

        if (!ModelState.IsValid)
        {
            followUp.CreatedDate = existing.CreatedDate;
            followUp.CreatedBy = existing.CreatedBy;

            await LoadFormDataAsync(
                followUp.CustomerId,
                followUp.LeadId,
                existing.AssignedTo);

            return View(followUp);
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        // Capture old values before modifying the entity.
        var oldValue = new
        {
            existing.FollowUpId,
            existing.CustomerId,
            existing.LeadId,
            existing.FollowUpDate,
            existing.FollowUpType,
            existing.Remarks,
            existing.Status,
            existing.AssignedTo
        };

        existing.CustomerId = followUp.CustomerId;
        existing.LeadId = followUp.LeadId;

        existing.FollowUpDate = DateTime.SpecifyKind(
            followUp.FollowUpDate,
            DateTimeKind.Utc);

        existing.FollowUpType = followUp.FollowUpType;
        existing.Remarks = followUp.Remarks;
        existing.Status = followUp.Status;

        if (User.IsInRole("Admin"))
            existing.AssignedTo = followUp.AssignedTo;

        existing.ModifiedDate = DateTime.UtcNow;

        var newValue = new
        {
            existing.FollowUpId,
            existing.CustomerId,
            existing.LeadId,
            existing.FollowUpDate,
            existing.FollowUpType,
            existing.Remarks,
            existing.Status,
            existing.AssignedTo
        };

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            "Update",
            "FollowUp",
            existing.FollowUpId.ToString(),
            oldValue,
            newValue);

        await transaction.CommitAsync();

        TempData["SuccessMessage"] =
            "Follow-up updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /FollowUps/Delete/5
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var followUp = await GetAccessibleFollowUps()
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (followUp == null)
            return NotFound();

        return View(followUp);
    }

    // POST: /FollowUps/Delete/5
    [Authorize(Roles = "Admin")]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var followUp = await GetAccessibleFollowUps()
            .FirstOrDefaultAsync(f => f.FollowUpId == id);

        if (followUp == null)
            return NotFound();

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        var oldValue = new
        {
            followUp.FollowUpId,
            followUp.CustomerId,
            followUp.LeadId,
            followUp.FollowUpDate,
            followUp.FollowUpType,
            followUp.Remarks,
            followUp.Status,
            followUp.AssignedTo
        };

        _context.FollowUps.Remove(followUp);

        await _context.SaveChangesAsync();

        await _auditLogService.LogAsync(
            "Delete",
            "FollowUp",
            followUp.FollowUpId.ToString(),
            oldValue: oldValue);

        await transaction.CommitAsync();

        TempData["SuccessMessage"] =
            "Follow-up deleted successfully.";

        return RedirectToAction(nameof(Index));
    }

    // Validate follow-up fields and related record ownership.
    private async Task ValidateFollowUpAsync(FollowUp followUp)
    {
        if (!AllowedTypes.Contains(followUp.FollowUpType))
        {
            ModelState.AddModelError(
                nameof(followUp.FollowUpType),
                "Select a valid follow-up type.");
        }

        if (!AllowedStatuses.Contains(followUp.Status))
        {
            ModelState.AddModelError(
                nameof(followUp.Status),
                "Select a valid follow-up status.");
        }

        bool hasCustomer = followUp.CustomerId.HasValue;
        bool hasLead = followUp.LeadId.HasValue;

        if (hasCustomer == hasLead)
        {
            ModelState.AddModelError(
                string.Empty,
                "Select exactly one Customer or Lead for this follow-up.");

            return;
        }

        if (hasCustomer)
        {
            var customerQuery = _context.Customers
                .Where(c => c.CustomerId == followUp.CustomerId);

            if (!User.IsInRole("Admin"))
            {
                customerQuery = customerQuery.Where(c =>
                    c.OwnerId == CurrentUserId);
            }

            if (!await customerQuery.AnyAsync())
            {
                ModelState.AddModelError(
                    nameof(followUp.CustomerId),
                    "The selected customer does not exist or you do not have access to it.");
            }
        }

        if (hasLead)
        {
            var leadQuery = _context.Leads
                .Where(l => l.LeadId == followUp.LeadId);

            if (!User.IsInRole("Admin"))
            {
                leadQuery = leadQuery.Where(l =>
                    l.AssignedTo == CurrentUserId);
            }

            if (!await leadQuery.AnyAsync())
            {
                ModelState.AddModelError(
                    nameof(followUp.LeadId),
                    "The selected lead does not exist or you do not have access to it.");
            }
        }
    }
}
