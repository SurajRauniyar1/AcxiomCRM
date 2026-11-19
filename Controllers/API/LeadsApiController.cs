
using System.Security.Claims;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.DTOs.Leads;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Controllers.Api;

[ApiController]
[Route("api/leads")]
[Authorize]
public class LeadsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AuditLogService _auditLogService;

    private static readonly string[] AllowedStatuses =
    {
        "New",
        "Contacted",
        "Qualified",
        "Unqualified",
        "Lost"
    };

    public LeadsApiController(
        ApplicationDbContext context,
        AuditLogService auditLogService)
    {
        _context = context;
        _auditLogService = auditLogService;
    }

    private string? CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    private bool IsAdmin =>
        User.IsInRole("Admin");

    private IQueryable<Lead> AccessibleLeads()
    {
        var query = _context.Leads.AsQueryable();

        if (!IsAdmin)
        {
            var userId = CurrentUserId;
            query = query.Where(x => x.AssignedTo == userId);
        }

        return query;
    }

    private static LeadResponseDto ToResponse(Lead lead)
    {
        return new LeadResponseDto
        {
            LeadId = lead.LeadId,
            LeadCode = lead.LeadCode,
            LeadName = lead.LeadName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Source = lead.Source,
            Status = lead.Status,
            Notes = lead.Notes,
            CreatedDate = lead.CreatedDate,
            Priority = null
        };
    }

    private static bool IsValidStatus(string status)
    {
        return AllowedStatuses.Any(
            x => x.Equals(status, StringComparison.OrdinalIgnoreCase));
    }

    // GET: api/leads
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var leads = await AccessibleLeads()
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        return Ok(leads.Select(ToResponse));
    }

    // GET: api/leads/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var lead = await AccessibleLeads()
            .FirstOrDefaultAsync(x => x.LeadId == id);

        if (lead == null)
            return NotFound(new { message = "Lead not found." });

        return Ok(ToResponse(lead));
    }

    // POST: api/leads
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateLeadDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
            return Unauthorized();

        var email = dto.Email?.Trim() ?? string.Empty;
        var phone = dto.Phone?.Trim() ?? string.Empty;
        var leadName = dto.LeadName.Trim();
        var source = dto.Source?.Trim() ?? string.Empty;

        var status = string.IsNullOrWhiteSpace(dto.Status)
            ? "New"
            : dto.Status.Trim();

        if (!IsValidStatus(status))
        {
            return BadRequest(new
            {
                message = "Invalid lead status.",
                allowedStatuses = AllowedStatuses
            });
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var emailExists = await _context.Leads
                .AnyAsync(x => x.Email == email);

            if (emailExists)
                return Conflict(new { message = "Email already exists." });
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            var phoneExists = await _context.Leads
                .AnyAsync(x => x.Phone == phone);

            if (phoneExists)
                return Conflict(new { message = "Phone already exists." });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var lead = new Lead
            {
                LeadCode = "TEMP",
                LeadName = leadName,
                Email = email,
                Phone = phone,
                CompanyName = dto.CompanyName?.Trim(),
                Source = source,
                Status = status,
                ExpectedValue = 0,
                Notes = dto.Notes,
                AssignedTo = CurrentUserId,
                CreatedBy = User.Identity?.Name,
                CreatedDate = DateTime.UtcNow,
                IsConverted = false
            };

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            lead.LeadCode = $"LEAD-{lead.LeadId:D5}";
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "Create",
                "Lead",
                lead.LeadId.ToString(),
                null,
                new
                {
                    lead.LeadCode,
                    lead.LeadName,
                    lead.Email,
                    lead.Phone,
                    lead.Status
                });

            await transaction.CommitAsync();

            return CreatedAtAction(
                nameof(GetById),
                new { id = lead.LeadId },
                ToResponse(lead));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // PUT: api/leads/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateLeadDto dto)
    {
        var lead = await AccessibleLeads()
            .FirstOrDefaultAsync(x => x.LeadId == id);

        if (lead == null)
            return NotFound(new { message = "Lead not found." });

        if (lead.IsConverted)
        {
            return Conflict(new
            {
                message = "Converted leads cannot be modified."
            });
        }

        var email = dto.Email?.Trim() ?? string.Empty;
        var phone = dto.Phone?.Trim() ?? string.Empty;
        var leadName = dto.LeadName.Trim();

        var source = dto.Source?.Trim() ?? string.Empty;

        var status = string.IsNullOrWhiteSpace(dto.Status)
            ? lead.Status
            : dto.Status.Trim();

        if (!IsValidStatus(status))
        {
            return BadRequest(new
            {
                message = "Invalid lead status.",
                allowedStatuses = AllowedStatuses
            });
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var emailExists = await _context.Leads
                .AnyAsync(x =>
                    x.Email == email &&
                    x.LeadId != id);

            if (emailExists)
                return Conflict(new { message = "Email already exists." });
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            var phoneExists = await _context.Leads
                .AnyAsync(x =>
                    x.Phone == phone &&
                    x.LeadId != id);

            if (phoneExists)
                return Conflict(new { message = "Phone already exists." });
        }

        var oldValue = new
        {
            lead.LeadName,
            lead.Email,
            lead.Phone,
            lead.CompanyName,
            lead.Source,
            lead.Status,
            lead.Notes
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            lead.LeadName = leadName;
            lead.Email = email;
            lead.Phone = phone;
            lead.CompanyName = dto.CompanyName?.Trim();
            lead.Source = source;
            lead.Status = status;
            lead.Notes = dto.Notes;
            lead.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var newValue = new
            {
                lead.LeadName,
                lead.Email,
                lead.Phone,
                lead.CompanyName,
                lead.Source,
                lead.Status,
                lead.Notes
            };

            await _auditLogService.LogAsync(
                "Update",
                "Lead",
                lead.LeadId.ToString(),
                oldValue,
                newValue);

            await transaction.CommitAsync();

            return Ok(ToResponse(lead));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // DELETE: api/leads/5
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await _context.Leads
            .FirstOrDefaultAsync(x => x.LeadId == id);

        if (lead == null)
            return NotFound(new { message = "Lead not found." });

        if (lead.IsConverted)
        {
            return Conflict(new
            {
                message = "Converted leads cannot be deleted."
            });
        }

        var oldValue = new
        {
            lead.LeadCode,
            lead.LeadName,
            lead.Email,
            lead.Phone,
            lead.Status
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "Delete",
                "Lead",
                lead.LeadId.ToString(),
                oldValue,
                null);

            await transaction.CommitAsync();

            return Ok(new { message = "Lead deleted successfully." });
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();

            return Conflict(new
            {
                message = "This lead cannot be deleted because related records exist."
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
