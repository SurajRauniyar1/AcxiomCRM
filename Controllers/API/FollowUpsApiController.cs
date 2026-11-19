
using System.Security.Claims;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.DTOs.FollowUps;
using AcxiomCRM.Web.Models;
using AcxiomCRM.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Web.Controllers.Api;

[ApiController]
[Route("api/followups")]
[Authorize]
public class FollowUpsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly AuditLogService _auditLogService;

    private static readonly string[] AllowedTypes =
    {
        "Call",
        "Meeting",
        "Email",
        "Task"
    };

    private static readonly string[] AllowedStatuses =
    {
        "Planned",
        "Completed",
        "Missed",
        "Cancelled"
    };

    public FollowUpsApiController(
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

    private IQueryable<FollowUp> AccessibleFollowUps()
    {
        var query = _context.FollowUps.AsQueryable();

        if (!IsAdmin)
        {
            var userId = CurrentUserId;
            query = query.Where(x => x.AssignedTo == userId);
        }

        return query;
    }

    private static FollowUpResponseDto ToResponse(FollowUp followUp)
    {
        return new FollowUpResponseDto
        {
            FollowUpId = followUp.FollowUpId,
            CustomerId = followUp.CustomerId,
            LeadId = followUp.LeadId,
            OpportunityId = followUp.OpportunityId,
            FollowUpDate = followUp.FollowUpDate,
            FollowUpType = followUp.FollowUpType,
            Remarks = followUp.Remarks,
            Status = followUp.Status,
            CreatedDate = followUp.CreatedDate
        };
    }

    private static bool IsValidType(string type)
    {
        return AllowedTypes.Any(
            x => x.Equals(type, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidStatus(string status)
    {
        return AllowedStatuses.Any(
            x => x.Equals(status, StringComparison.OrdinalIgnoreCase));
    }

    private static DateTime AsUtc(DateTime date)
    {
        return DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }

    private async Task<bool> HasAccessibleRelatedRecords(
        int? customerId,
        int? leadId,
        int? opportunityId)
    {
        // Exactly one Customer or Lead is required.
        if ((customerId.HasValue && leadId.HasValue) ||
            (!customerId.HasValue && !leadId.HasValue))
        {
            return false;
        }

        if (customerId.HasValue)
        {
            var customerQuery = _context.Customers
                .Where(x => x.CustomerId == customerId.Value);

            if (!IsAdmin)
            {
                customerQuery = customerQuery.Where(
                    x => x.OwnerId == CurrentUserId);
            }

            if (!await customerQuery.AnyAsync())
                return false;
        }

        if (leadId.HasValue)
        {
            var leadQuery = _context.Leads
                .Where(x => x.LeadId == leadId.Value);

            if (!IsAdmin)
            {
                leadQuery = leadQuery.Where(
                    x => x.AssignedTo == CurrentUserId);
            }

            if (!await leadQuery.AnyAsync())
                return false;
        }

        // Opportunity is optional, but must be accessible if supplied.
        if (opportunityId.HasValue)
        {
            var opportunityQuery = _context.Opportunities
                .Where(x => x.OpportunityId == opportunityId.Value);

            if (!IsAdmin)
            {
                opportunityQuery = opportunityQuery.Where(
                    x => x.AssignedTo == CurrentUserId);
            }

            if (!await opportunityQuery.AnyAsync())
                return false;
        }

        return true;
    }

    // GET: api/followups
    // Optional filters: status, fromDate, toDate
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        var query = AccessibleFollowUps();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(
                x => x.Status == status.Trim());
        }

        if (fromDate.HasValue)
        {
            var start = AsUtc(fromDate.Value);
            query = query.Where(x => x.FollowUpDate >= start);
        }

        if (toDate.HasValue)
        {
            var end = AsUtc(toDate.Value);
            query = query.Where(x => x.FollowUpDate <= end);
        }

        var followUps = await query
            .OrderBy(x => x.FollowUpDate)
            .ToListAsync();

        return Ok(followUps.Select(ToResponse));
    }

    // GET: api/followups/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var followUp = await AccessibleFollowUps()
            .FirstOrDefaultAsync(x => x.FollowUpId == id);

        if (followUp == null)
            return NotFound(new { message = "Follow-up not found." });

        return Ok(ToResponse(followUp));
    }

    // POST: api/followups
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateFollowUpDto dto)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
            return Unauthorized();

        if (!IsValidType(dto.FollowUpType))
        {
            return BadRequest(new
            {
                message = "Invalid follow-up type.",
                allowedTypes = AllowedTypes
            });
        }

        if (!IsValidStatus(dto.Status))
        {
            return BadRequest(new
            {
                message = "Invalid follow-up status.",
                allowedStatuses = AllowedStatuses
            });
        }

        var followUpDate = AsUtc(dto.FollowUpDate);

        if (followUpDate <= DateTime.UtcNow)
        {
            return BadRequest(new
            {
                message = "Follow-up date must be in the future."
            });
        }

        if (!await HasAccessibleRelatedRecords(
            dto.CustomerId,
            dto.LeadId,
            dto.OpportunityId))
        {
            return BadRequest(new
            {
                message = "Select exactly one accessible Customer or Lead. Any supplied Opportunity must also be accessible."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var followUp = new FollowUp
            {
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                OpportunityId = dto.OpportunityId,
                FollowUpDate = followUpDate,
                FollowUpType = dto.FollowUpType.Trim(),
                Remarks = dto.Remarks?.Trim(),
                Status = dto.Status.Trim(),
                AssignedTo = CurrentUserId,
                CreatedBy = User.Identity?.Name,
                CreatedDate = DateTime.UtcNow
            };

            _context.FollowUps.Add(followUp);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "Create",
                "FollowUp",
                followUp.FollowUpId.ToString(),
                null,
                new
                {
                    followUp.CustomerId,
                    followUp.LeadId,
                    followUp.OpportunityId,
                    followUp.FollowUpDate,
                    followUp.FollowUpType,
                    followUp.Status
                });

            await transaction.CommitAsync();

            return CreatedAtAction(
                nameof(GetById),
                new { id = followUp.FollowUpId },
                ToResponse(followUp));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // PUT: api/followups/1
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateFollowUpDto dto)
    {
        var followUp = await AccessibleFollowUps()
            .FirstOrDefaultAsync(x => x.FollowUpId == id);

        if (followUp == null)
            return NotFound(new { message = "Follow-up not found." });

        if (!IsValidType(dto.FollowUpType))
        {
            return BadRequest(new
            {
                message = "Invalid follow-up type.",
                allowedTypes = AllowedTypes
            });
        }

        if (!IsValidStatus(dto.Status))
        {
            return BadRequest(new
            {
                message = "Invalid follow-up status.",
                allowedStatuses = AllowedStatuses
            });
        }

        var followUpDate = AsUtc(dto.FollowUpDate);

        if (!await HasAccessibleRelatedRecords(
            dto.CustomerId,
            dto.LeadId,
            dto.OpportunityId))
        {
            return BadRequest(new
            {
                message = "Select exactly one accessible Customer or Lead. Any supplied Opportunity must also be accessible."
            });
        }

        var oldValue = new
        {
            followUp.CustomerId,
            followUp.LeadId,
            followUp.OpportunityId,
            followUp.FollowUpDate,
            followUp.FollowUpType,
            followUp.Remarks,
            followUp.Status
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            followUp.CustomerId = dto.CustomerId;
            followUp.LeadId = dto.LeadId;
            followUp.OpportunityId = dto.OpportunityId;
            followUp.FollowUpDate = followUpDate;
            followUp.FollowUpType = dto.FollowUpType.Trim();
            followUp.Remarks = dto.Remarks?.Trim();
            followUp.Status = dto.Status.Trim();
            followUp.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var newValue = new
            {
                followUp.CustomerId,
                followUp.LeadId,
                followUp.OpportunityId,
                followUp.FollowUpDate,
                followUp.FollowUpType,
                followUp.Remarks,
                followUp.Status
            };

            await _auditLogService.LogAsync(
                "Update",
                "FollowUp",
                followUp.FollowUpId.ToString(),
                oldValue,
                newValue);

            await transaction.CommitAsync();

            return Ok(ToResponse(followUp));
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // DELETE: api/followups/1
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var followUp = await _context.FollowUps
            .FirstOrDefaultAsync(x => x.FollowUpId == id);

        if (followUp == null)
            return NotFound(new { message = "Follow-up not found." });

        var oldValue = new
        {
            followUp.CustomerId,
            followUp.LeadId,
            followUp.OpportunityId,
            followUp.FollowUpDate,
            followUp.FollowUpType,
            followUp.Status
        };

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            _context.FollowUps.Remove(followUp);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                "Delete",
                "FollowUp",
                followUp.FollowUpId.ToString(),
                oldValue,
                null);

            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Follow-up deleted successfully."
            });
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();

            return Conflict(new
            {
                message = "This follow-up cannot be deleted because related records exist."
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
