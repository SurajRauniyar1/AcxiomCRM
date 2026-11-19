
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
public class LeadsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly AuditLogService _auditLogService;
    private readonly ILogger<LeadsController> _logger;

    private const int PageSize = 10;

    private static readonly string[] AllowedStatuses =
    {
        "New",
        "Contacted",
        "Qualified",
        "Unqualified",
        "Lost"
    };

    public LeadsController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        AuditLogService auditLogService,
        ILogger<LeadsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("User ID not found.");

    private bool IsAdmin => User.IsInRole("Admin");

    // Admin sees all leads.
    // Manager and SalesExecutive see assigned leads.
    private IQueryable<Lead> GetAccessibleLeads()
    {
        var query = _context.Leads.AsQueryable();

        if (IsAdmin)
            return query;

        return query.Where(l => l.AssignedTo == CurrentUserId);
    }

    // Only Admin can assign leads to other users.
    private async Task LoadUsersAsync(string? selectedUserId = null)
    {
        if (IsAdmin)
        {
            ViewBag.AssignedUsers = await _userManager.Users
                .OrderBy(u => u.Email)
                .Select(u => new SelectListItem
                {
                    Text = u.Email ?? u.UserName ?? u.Id,
                    Value = u.Id,
                    Selected = u.Id == selectedUserId
                })
                .ToListAsync();
        }
        else
        {
            ViewBag.AssignedUsers = new List<SelectListItem>
            {
                new()
                {
                    Text = User.Identity?.Name ?? "Current User",
                    Value = CurrentUserId,
                    Selected = true
                }
            };
        }
    }

    // GET: /Leads
    public async Task<IActionResult> Index(
        string? search,
        string? status,
        int page = 1)
    {
        var leads = GetAccessibleLeads();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            leads = leads.Where(l =>
                l.LeadName.Contains(search) ||
                l.LeadCode.Contains(search) ||
                l.Email.Contains(search) ||
                l.Phone.Contains(search) ||
                (l.CompanyName != null &&
                 l.CompanyName.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(status))
            leads = leads.Where(l => l.Status == status);

        var totalRecords = await leads.CountAsync();

        var totalPages = (int)Math.Ceiling(
            totalRecords / (double)PageSize);

        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var result = await leads
            .OrderByDescending(l => l.CreatedDate)
            .ThenByDescending(l => l.LeadId)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        ViewData["Search"] = search;
        ViewData["Status"] = status;

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalRecords = totalRecords;

        var userIds = result
            .Where(l => !string.IsNullOrEmpty(l.AssignedTo))
            .Select(l => l.AssignedTo!)
            .Distinct()
            .ToList();

        ViewBag.UserNames = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(
                u => u.Id,
                u => u.Email ?? u.UserName ?? u.Id);

        return View(result);
    }

    // GET: /Leads/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var lead = await GetAccessibleLeads()
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (lead == null)
            return NotFound();

        if (!string.IsNullOrEmpty(lead.AssignedTo))
        {
            var assignedUser =
                await _userManager.FindByIdAsync(lead.AssignedTo);

            ViewBag.AssignedUser =
                assignedUser?.Email ?? "Unknown user";
        }

        if (lead.ConvertedCustomerId.HasValue)
        {
            ViewBag.ConvertedCustomer =
                await _context.Customers.FindAsync(
                    lead.ConvertedCustomerId.Value);
        }

        return View(lead);
    }

    // GET: /Leads/Create
    public async Task<IActionResult> Create()
    {
        await LoadUsersAsync(
            IsAdmin ? null : CurrentUserId);

        return View();
    }

    // POST: /Leads/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Lead lead)
    {
        ModelState.Remove(nameof(Lead.LeadCode));
        ModelState.Remove(nameof(Lead.CreatedDate));
        ModelState.Remove(nameof(Lead.CreatedBy));
        ModelState.Remove(nameof(Lead.ModifiedDate));
        ModelState.Remove(nameof(Lead.IsConverted));
        ModelState.Remove(nameof(Lead.ConvertedCustomerId));

        if (!IsAdmin)
        {
            ModelState.Remove(nameof(Lead.AssignedTo));
            lead.AssignedTo = CurrentUserId;
        }
        else if (string.IsNullOrWhiteSpace(lead.AssignedTo))
        {
            ModelState.AddModelError(
                nameof(lead.AssignedTo),
                "Please select a user.");
        }

        lead.Email = lead.Email?.Trim() ?? string.Empty;
        lead.Phone = lead.Phone?.Trim() ?? string.Empty;

        if (await _context.Leads.AnyAsync(l =>
            l.Email == lead.Email))
        {
            ModelState.AddModelError(
                nameof(lead.Email),
                "This email address is already registered as a lead.");
        }

        if (await _context.Leads.AnyAsync(l =>
            l.Phone == lead.Phone))
        {
            ModelState.AddModelError(
                nameof(lead.Phone),
                "This phone number is already registered as a lead.");
        }

        if (!string.IsNullOrEmpty(lead.AssignedTo) &&
            await _userManager.FindByIdAsync(lead.AssignedTo) == null)
        {
            ModelState.AddModelError(
                nameof(lead.AssignedTo),
                "Please select a valid user.");
        }

        if (!ModelState.IsValid)
        {
            await LoadUsersAsync(lead.AssignedTo);
            return View(lead);
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            lead.LeadCode = $"TMP-{Guid.NewGuid():N}"[..20];
            lead.CreatedDate = DateTime.UtcNow;
            lead.CreatedBy = User.Identity?.Name;
            lead.ModifiedDate = null;
            lead.Status = "New";
            lead.IsConverted = false;
            lead.ConvertedCustomerId = null;

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            lead.LeadCode = $"LEAD-{lead.LeadId:D4}";
            await _context.SaveChangesAsync();

            // Audit: Lead created.
            await _auditLogService.LogAsync(
                action: "Create",
                entityName: "Lead",
                recordId: lead.LeadId.ToString(),
                newValue: new
                {
                    lead.LeadId,
                    lead.LeadCode,
                    lead.LeadName,
                    lead.Email,
                    lead.Phone,
                    lead.CompanyName,
                    lead.Source,
                    lead.Status,
                    lead.ExpectedValue,
                    lead.AssignedTo
                });

            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "Lead created successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            _logger.LogError(ex, "Error creating lead.");

            _context.Entry(lead).State = EntityState.Detached;

            ModelState.AddModelError(
                string.Empty,
                "Unable to create lead. Please check the application logs.");

            await LoadUsersAsync(lead.AssignedTo);
            return View(lead);
        }
    }

    // GET: /Leads/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var lead = await GetAccessibleLeads()
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (lead == null)
            return NotFound();

        if (lead.IsConverted)
        {
            TempData["ErrorMessage"] =
                "A converted lead cannot be edited.";

            return RedirectToAction(nameof(Index));
        }

        await LoadUsersAsync(lead.AssignedTo);
        return View(lead);
    }

    // POST: /Leads/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Lead lead)
    {
        if (id != lead.LeadId)
            return NotFound();

        var existingLead = await GetAccessibleLeads()
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (existingLead == null)
            return NotFound();

        if (existingLead.IsConverted)
            return BadRequest("Converted leads cannot be edited.");

        ModelState.Remove(nameof(Lead.LeadCode));
        ModelState.Remove(nameof(Lead.CreatedDate));
        ModelState.Remove(nameof(Lead.CreatedBy));
        ModelState.Remove(nameof(Lead.ModifiedDate));
        ModelState.Remove(nameof(Lead.IsConverted));
        ModelState.Remove(nameof(Lead.ConvertedCustomerId));

        if (!IsAdmin)
        {
            ModelState.Remove(nameof(Lead.AssignedTo));
            lead.AssignedTo = existingLead.AssignedTo;
        }

        lead.Email = lead.Email?.Trim() ?? string.Empty;
        lead.Phone = lead.Phone?.Trim() ?? string.Empty;

        if (!AllowedStatuses.Contains(lead.Status))
        {
            ModelState.AddModelError(
                nameof(lead.Status),
                "Please select a valid lead status.");
        }

        if (await _context.Leads.AnyAsync(l =>
            l.Email == lead.Email && l.LeadId != id))
        {
            ModelState.AddModelError(
                nameof(lead.Email),
                "This email address is already used by another lead.");
        }

        if (await _context.Leads.AnyAsync(l =>
            l.Phone == lead.Phone && l.LeadId != id))
        {
            ModelState.AddModelError(
                nameof(lead.Phone),
                "This phone number is already used by another lead.");
        }

        if (IsAdmin &&
            !string.IsNullOrEmpty(lead.AssignedTo) &&
            await _userManager.FindByIdAsync(lead.AssignedTo) == null)
        {
            ModelState.AddModelError(
                nameof(lead.AssignedTo),
                "Please select a valid user.");
        }

        if (!ModelState.IsValid)
        {
            lead.LeadCode = existingLead.LeadCode;
            lead.CreatedDate = existingLead.CreatedDate;
            lead.CreatedBy = existingLead.CreatedBy;
            lead.IsConverted = existingLead.IsConverted;
            lead.ConvertedCustomerId = existingLead.ConvertedCustomerId;

            await LoadUsersAsync(existingLead.AssignedTo);
            return View(lead);
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            // Capture old values before modifying the lead.
            var oldValue = new
            {
                existingLead.LeadCode,
                existingLead.LeadName,
                existingLead.Email,
                existingLead.Phone,
                existingLead.CompanyName,
                existingLead.Source,
                existingLead.Status,
                existingLead.ExpectedValue,
                existingLead.AssignedTo,
                existingLead.Notes
            };

            existingLead.LeadName = lead.LeadName;
            existingLead.Email = lead.Email;
            existingLead.Phone = lead.Phone;
            existingLead.CompanyName = lead.CompanyName;
            existingLead.Source = lead.Source;
            existingLead.Status = lead.Status;
            existingLead.ExpectedValue = lead.ExpectedValue;
            existingLead.Notes = lead.Notes;

            if (IsAdmin)
                existingLead.AssignedTo = lead.AssignedTo;

            existingLead.ModifiedDate = DateTime.UtcNow;

            var newValue = new
            {
                existingLead.LeadCode,
                existingLead.LeadName,
                existingLead.Email,
                existingLead.Phone,
                existingLead.CompanyName,
                existingLead.Source,
                existingLead.Status,
                existingLead.ExpectedValue,
                existingLead.AssignedTo,
                existingLead.Notes
            };

            await _context.SaveChangesAsync();

            // Audit: Lead updated.
            await _auditLogService.LogAsync(
                action: "Update",
                entityName: "Lead",
                recordId: existingLead.LeadId.ToString(),
                oldValue: oldValue,
                newValue: newValue);

            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "Lead updated successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            _logger.LogError(
                ex,
                "Error updating lead {LeadId}.",
                id);

            ModelState.AddModelError(
                string.Empty,
                "Unable to update lead. Please try again.");

            await LoadUsersAsync(existingLead.AssignedTo);
            return View(lead);
        }
    }

    // GET: /Leads/Delete/5
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var lead = await GetAccessibleLeads()
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (lead == null)
            return NotFound();

        if (lead.IsConverted)
            return BadRequest("Converted leads cannot be deleted.");

        return View(lead);
    }

    // POST: /Leads/Delete/5
    [Authorize(Roles = "Admin")]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var lead = await GetAccessibleLeads()
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (lead == null)
            return NotFound();

        if (lead.IsConverted)
        {
            TempData["ErrorMessage"] =
                "Converted leads cannot be deleted.";

            return RedirectToAction(nameof(Index));
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var oldValue = new
            {
                lead.LeadId,
                lead.LeadCode,
                lead.LeadName,
                lead.Email,
                lead.Phone,
                lead.CompanyName,
                lead.Source,
                lead.Status,
                lead.ExpectedValue,
                lead.AssignedTo,
                lead.Notes
            };

            // Audit before deleting the lead.
            await _auditLogService.LogAsync(
                action: "Delete",
                entityName: "Lead",
                recordId: lead.LeadId.ToString(),
                oldValue: oldValue);

            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "Lead deleted successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            _logger.LogError(
                ex,
                "Error deleting lead {LeadId}.",
                id);

            TempData["ErrorMessage"] =
                "Unable to delete lead. Please try again.";

            return RedirectToAction(nameof(Index));
        }
    }

    // GET: /Leads/Convert/5
    public async Task<IActionResult> Convert(int? id)
    {
        if (id == null)
            return NotFound();

        var lead = await GetAccessibleLeads()
            .FirstOrDefaultAsync(l => l.LeadId == id);

        if (lead == null)
            return NotFound();

        if (lead.IsConverted || lead.Status != "Qualified")
        {
            TempData["ErrorMessage"] =
                "Only qualified, unconverted leads can be converted.";

            return RedirectToAction(nameof(Index));
        }

        var duplicateCustomer = await _context.Customers.AnyAsync(c =>
            c.Email == lead.Email || c.Phone == lead.Phone);

        if (duplicateCustomer)
        {
            TempData["ErrorMessage"] =
                "A customer with this email or phone number already exists.";

            return RedirectToAction(nameof(Index));
        }

        return View(lead);
    }

    // POST: /Leads/ConvertConfirmed/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertConfirmed(int id)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var lead = await GetAccessibleLeads()
                .FirstOrDefaultAsync(l => l.LeadId == id);

            if (lead == null)
                return NotFound();

            if (lead.IsConverted || lead.Status != "Qualified")
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "Only qualified, unconverted leads can be converted.";

                return RedirectToAction(nameof(Index));
            }

            var duplicateCustomer = await _context.Customers.AnyAsync(c =>
                c.Email == lead.Email || c.Phone == lead.Phone);

            if (duplicateCustomer)
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "A customer with this email or phone number already exists.";

                return RedirectToAction(nameof(Index));
            }

            var oldLeadValue = new
            {
                lead.LeadId,
                lead.LeadCode,
                lead.LeadName,
                lead.Email,
                lead.Phone,
                lead.CompanyName,
                lead.Source,
                lead.Status,
                lead.ExpectedValue,
                lead.AssignedTo,
                lead.IsConverted,
                lead.ConvertedCustomerId
            };

            var customer = new Customer
            {
                CustomerCode = $"TMP-{Guid.NewGuid():N}"[..20],
                CustomerName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                Status = "Active",
                OwnerId = lead.AssignedTo,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name,
                Notes = lead.Notes
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            customer.CustomerCode =
                $"CUST-{customer.CustomerId:D4}";

            lead.Status = "Converted";
            lead.IsConverted = true;
            lead.ConvertedCustomerId = customer.CustomerId;
            lead.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var newLeadValue = new
            {
                lead.LeadId,
                lead.LeadCode,
                lead.LeadName,
                lead.Email,
                lead.Phone,
                lead.CompanyName,
                lead.Source,
                lead.Status,
                lead.ExpectedValue,
                lead.AssignedTo,
                lead.IsConverted,
                lead.ConvertedCustomerId
            };

            // Audit: Customer created through lead conversion.
            await _auditLogService.LogAsync(
                action: "Create",
                entityName: "Customer",
                recordId: customer.CustomerId.ToString(),
                newValue: new
                {
                    customer.CustomerId,
                    customer.CustomerCode,
                    customer.CustomerName,
                    customer.Email,
                    customer.Phone,
                    customer.CompanyName,
                    customer.Status,
                    customer.OwnerId
                });

            // Audit: Lead converted.
            await _auditLogService.LogAsync(
                action: "Convert",
                entityName: "Lead",
                recordId: lead.LeadId.ToString(),
                oldValue: oldLeadValue,
                newValue: newLeadValue);

            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                $"Lead converted successfully. Customer code: {customer.CustomerCode}";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            _logger.LogError(
                ex,
                "Error converting lead {LeadId}.",
                id);

            TempData["ErrorMessage"] =
                "Lead conversion failed. Please check the application logs.";

            return RedirectToAction(nameof(Index));
        }
    }
}
